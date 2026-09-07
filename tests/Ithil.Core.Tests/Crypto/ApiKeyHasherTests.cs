using AwesomeAssertions;
using Ithil.Core.Crypto;

namespace Ithil.Core.Tests.Crypto;

public sealed class ApiKeyHasherTests
{
    [Fact]
    public void Hash_SameInput_ReturnsSameHash()
    {
        var first = ApiKeyHasher.Hash("my-api-key");
        var second = ApiKeyHasher.Hash("my-api-key");

        first.Should().Be(second);
    }

    [Fact]
    public void Hash_DifferentInputs_ReturnDifferentHashes()
    {
        var hash1 = ApiKeyHasher.Hash("key-one");
        var hash2 = ApiKeyHasher.Hash("key-two");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Hash_OutputIsLowercaseHex()
    {
        var hash = ApiKeyHasher.Hash("test-key");

        // SHA-256 hex is always 64 characters
        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public void Hash_EmptyString_DoesNotThrow()
    {
        var act = () => ApiKeyHasher.Hash(string.Empty);

        act.Should().NotThrow();
    }

    [Fact]
    public void Hash_EmptyString_ReturnsKnownSha256()
    {
        // SHA-256 of "" is a well-known constant — pins the algorithm choice
        var hash = ApiKeyHasher.Hash(string.Empty);

        hash.Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }
}
