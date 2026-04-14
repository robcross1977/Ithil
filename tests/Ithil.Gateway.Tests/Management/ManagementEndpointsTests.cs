using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Management;
using Ithil.Management.Models;
using Ithil.Management.Services;
using LanguageExt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace Ithil.Gateway.Tests.Management;

public class ManagementEndpointsTests
{
    private const string SigningKeyValue = "test-signing-key-for-management-auth-tests-must-be-long";
    private const string Issuer = "test-issuer";
    private const string Audience = "test-audience";

    private readonly IAgentManagementService _managementSvc =
        Substitute.For<IAgentManagementService>();
    private readonly IBudgetQueryService _budgetSvc =
        Substitute.For<IBudgetQueryService>();
    private readonly IToolRegistry _toolRegistry =
        Substitute.For<IToolRegistry>();

    private async Task<WebApplication> BuildTestAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKeyValue));

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = Issuer,
                    ValidateAudience = true,
                    ValidAudience = Audience,
                    ValidateLifetime = true,
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddAuthorizationBuilder().AddManagementPolicy();
        builder.Services.AddSingleton(_managementSvc);
        builder.Services.AddSingleton(_budgetSvc);
        builder.Services.AddSingleton(_toolRegistry);

        var app = builder.Build();

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapManagementEndpoints();

        await app.StartAsync();
        return app;
    }

    // Builds a JWT to simulate either an agent token (default) or an admin token (when scope is provided).
    private static string BuildToken(string? scope = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKeyValue));
        var claims = new Dictionary<string, object>();

        if (scope is null)
        {
            claims["agent_id"] = "test-agent";
        }
        else
        {
            claims["scope"] = scope;
            claims["sub"] = "test-admin-user";
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Claims = claims,
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });
    }

    [Fact]
    public async Task Returns401_WhenNoToken()
    {
        await using var app = await BuildTestAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/management/agents");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Returns403_WhenScopeIsNotAdmin()
    {
        await using var app = await BuildTestAppAsync();
        var client = app.GetTestClient();
        // Valid JWT but no admin scope — authenticated but not authorized.
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", BuildToken(scope: null));

        var response = await client.GetAsync("/management/agents");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Returns201_OnSuccessfulAgentCreate()
    {
        _managementSvc.CreateAsync(Arg.Any<CreateAgentRequest>())
            .Returns(Either<ManagementError, CreateAgentResponse>.Right(new CreateAgentResponse
            {
                AgentId = "agt_new001",
                ApiKey  = "ithil_live_abc",
                Label   = "New Agent",
                IsActive = true,
            }));

        await using var app = await BuildTestAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", BuildToken(scope: "admin"));

        var body = JsonSerializer.Serialize(new CreateAgentRequest { Label = "New Agent" });
        var response = await client.PostAsync(
            "/management/agents",
            new StringContent(body, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Returns404_OnGetUnknownAgent()
    {
        _managementSvc.GetAsync("unknown")
            .Returns(Either<ManagementError, AgentResponse>.Left(
                new ManagementError.NotFound("unknown")));

        await using var app = await BuildTestAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", BuildToken(scope: "admin"));

        var response = await client.GetAsync("/management/agents/unknown");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
