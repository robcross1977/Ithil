using FluentAssertions;
using Ithil.Gateway.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Ithil.Gateway.Tests.Identity;

public class JwtIdentityResolverTests
{
    private static readonly SymmetricSecurityKey TestKey =
        new(Encoding.UTF8.GetBytes("test-signing-key-must-be-at-least-32-bytes!"));

    private static readonly TokenValidationParameters ValidParams = new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = TestKey,
        ValidateIssuer = true,
        ValidIssuer = "test-issuer",
        ValidateAudience = true,
        ValidAudience = "test-audience",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    private static string CreateToken(
        string agentId = "claude-prod-01",
        DateTime? expires = null,
        SigningCredentials? signingCredentials = null)
    {
        var handler = new JsonWebTokenHandler();

        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object> { { "agent_id", agentId } },
            Expires = expires ?? DateTime.UtcNow.AddHours(1),
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningCredentials = signingCredentials
                ?? new SigningCredentials(TestKey, SecurityAlgorithms.HmacSha256)
        });
    }

    private static HttpContext ContextWithBearer(string token)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = $"Bearer {token}";
        return ctx;
    }

    [Fact]
    public async Task ReturnsAgentId_ForValidToken()
    {
        var token = CreateToken("claude-prod-01");
        var result = await new JwtIdentityResolver(ValidParams).TryResolveAsync(ContextWithBearer(token));
        result.IsSome.Should().BeTrue();
        result.IfSome(v => v.Should().Be("claude-prod-01"));
    }

    [Fact]
    public async Task ReturnsNone_ForExpiredToken()
    {
        var token = CreateToken(expires: DateTime.UtcNow.AddHours(-1));
        var result = await new JwtIdentityResolver(ValidParams).TryResolveAsync(ContextWithBearer(token));
        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task ReturnsNone_ForInvalidSignature()
    {
        var wrongKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("wrong-signing-key-must-be-32-bytes!!"));
        var token = CreateToken(signingCredentials: new SigningCredentials(wrongKey, SecurityAlgorithms.HmacSha256));
        var result = await new JwtIdentityResolver(ValidParams).TryResolveAsync(ContextWithBearer(token));

        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task ReturnsNone_WhenNoBearerHeader()
    {
        var result = await new JwtIdentityResolver(ValidParams).TryResolveAsync(new DefaultHttpContext());

        result.IsNone.Should().BeTrue();
    }
}

