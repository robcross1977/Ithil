using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Ithil.Gateway.Tests.Mcp;

public class McpAuthorizationTests
{
    private static readonly SymmetricSecurityKey TestKey = new(
        Encoding.UTF8.GetBytes("test-signing-key-must-be-at-least-32-bytes!")
    );

    private static async Task<WebApplication> BuildAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = TestKey,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                };
            });
        builder.Services.AddAuthorization();
        builder.Services.AddMcpServer()
            .WithHttpTransport(options =>
                options.ConfigureSessionOptions = (_, _, _) => Task.CompletedTask);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapMcp("/mcp").RequireAuthorization();

        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task PostMcp_Returns401_WithNoToken()
    {
        await using var app = await BuildAppAsync();
        var client = app.GetTestClient();

        var response = await client.PostAsync("/mcp",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
