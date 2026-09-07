using AwesomeAssertions;
using Ithil.Core.Crypto;
using Ithil.Management.Repositories;

namespace Ithil.Management.Tests.Repositories;

public class InMemoryApiKeyRepositoryTests
{
    private static InMemoryApiKeyRepository CreateRepo() => new();

    [Fact]
    public async Task InMemoryApiKeyRepository_Create_ReturnsPlaintextKey_WithCorrectPrefix()
    {
        var key = await CreateRepo().CreateAsync("agent-1");

        key.Should().StartWith("ithil_live_");
        key.Should().HaveLength(75);
    }

    [Fact]
    public async Task InMemoryApiKeyRepository_Create_StoresHash_NotPlaintext()
    {
        var repo = CreateRepo();
        var plaintext = await repo.CreateAsync("agent-1");
        var hash = ApiKeyHasher.Hash(plaintext);

        var found = await repo.FindByHashedKeyAsync(hash);
        found.IsSome.Should().BeTrue();
        // The stored lookup key is the hash, not the plaintext
        plaintext.Should().NotBe(hash);
    }

    [Fact]
    public async Task InMemoryApiKeyRepository_FindByHash_ReturnsSome_ForKnownHash()
    {
        var repo = CreateRepo();
        var plaintext = await repo.CreateAsync("agent-1");

        var result = await repo.FindByHashedKeyAsync(ApiKeyHasher.Hash(plaintext));

        result.IsSome.Should().BeTrue();
        ((string)result).Should().Be("agent-1");
    }

    [Fact]
    public async Task InMemoryApiKeyRepository_FindByHash_ReturnsNone_ForUnknownHash()
    {
        var result = await CreateRepo().FindByHashedKeyAsync("nonexistent-hash");

        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task InMemoryApiKeyRepository_Delete_RemovesHash()
    {
        var repo = CreateRepo();
        var plaintext = await repo.CreateAsync("agent-1");
        var hash = ApiKeyHasher.Hash(plaintext);

        await repo.DeleteAsync(hash);

        var result = await repo.FindByHashedKeyAsync(hash);
        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task InMemoryApiKeyRepository_Delete_IsNoOp_ForUnknownHash()
    {
        var act = async () => await CreateRepo().DeleteAsync("nonexistent-hash");

        await act.Should().NotThrowAsync();
    }
}
