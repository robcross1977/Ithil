using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Ithil.Core.Interfaces;
using Ithil.Gateway.Identity;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Ithil.Gateway.Tests.Identity;

public class ApiKeyIdentityResolverTests
{
    private readonly IApiKeyRepository _repo = Substitute.For<IApiKeyRepository>();

    private static HttpContext ContextWithKey(string key)
    {
        DefaultHttpContext ctx = new();
        ctx.Request.Headers["X-Api-Key"] = key;
        return ctx;
    }

    private static string Sha256Hex(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    [Fact]
    public async Task ReturnsAgentId_ForKnownKey()
    {
        _repo.FindByHashedKeyAsync(Arg.Any<string>()).Returns(Option<string>.Some("agent-01"));

        var result = await new ApiKeyIdentityResolver(_repo).TryResolveAsync(
            ContextWithKey("my-row-key")
        );

        result.IsSome.Should().BeTrue();
        result.IfSome(v => v.Should().Be("agent-01"));
    }

    [Fact]
    public async Task ReturnsNone_ForUnknownKey()
    {
        _repo.FindByHashedKeyAsync(Arg.Any<string>()).Returns(Option<String>.None);

        var result = await new ApiKeyIdentityResolver(_repo).TryResolveAsync(
            ContextWithKey("unknown-key")
        );

        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task HashesKeyBeforeLookup()
    {
        var rawKey = "my-raw-key";
        _repo.FindByHashedKeyAsync(Arg.Any<string>()).Returns(Option<string>.None);

        await new ApiKeyIdentityResolver(_repo).TryResolveAsync(ContextWithKey(rawKey));

        await _repo.Received(1).FindByHashedKeyAsync(Sha256Hex(rawKey));
    }
}
