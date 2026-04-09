using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Server;

namespace Ithil.Gateway.Tests.Mcp;

/// <summary>
/// Verifies that the /mcp endpoint enforces JWT authorization before the SDK processes any request.
/// These tests cover the auth layer only — MCP session behavior is tested in McpSessionConfigurationTests.
/// </summary>
public class McpEndpointAuthTests
{
    // Shared signing key used by both the test server and the token factory.
    private const string SigningKeyValue = "test-signing-key-for-mcp-auth-unit-tests-must-be-long";
    private const string Issuer = "test-issuer";
    private const string Audience = "test-audience";

    // Builds a minimal ASP.NET Core test app that mirrors the real gateway's auth+MCP setup.
    // Callers must dispose the returned WebApplication with `await using`.
    private static async Task<WebApplication> BuildTestAppAsync()
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

        // Use a no-op session callback so the SDK does not require other Ithil services.
        builder.Services
            .AddMcpServer()
            .WithHttpTransport(options =>
                options.ConfigureSessionOptions = (_, _, _) => Task.CompletedTask);

        var app = builder.Build();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapMcp("/mcp").RequireAuthorization();

        await app.StartAsync();
        return app;
    }

    private static string BuildValidToken(string agentId = "agent-A")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKeyValue));
        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object> { { "agent_id", agentId } },
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });
    }

    [Fact]
    public async Task McpPost_Returns401_WithNoToken()
    {
        await using var app = await BuildTestAppAsync();
        var client = app.GetTestClient();

        var response = await client.PostAsync("/mcp", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpPost_Returns401_WithInvalidToken()
    {
        await using var app = await BuildTestAppAsync();
        var client = app.GetTestClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "invalid.token.value");

        var response = await client.PostAsync("/mcp", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpPost_PassesAuthCheck_WithValidToken()
    {
        await using var app = await BuildTestAppAsync();
        var client = app.GetTestClient();
        var token = BuildValidToken();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/mcp", null);

        // Auth passes — the SDK may return 4xx for a missing/invalid MCP body, but not 401/403.
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }
}
