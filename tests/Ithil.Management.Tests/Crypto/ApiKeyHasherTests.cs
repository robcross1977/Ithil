using AwesomeAssertions;
using Ithil.Core.Crypto;
using System.Security.Cryptography;
using System.Text;

namespace Ithil.Management.Tests.Crypto;

public class ApiKeyHasherTests
{
    [Fact]
    public void ApiKeyHasher_ProducesSameHash_ForSameInput()
    {
        var hash1 = ApiKeyHasher.Hash("ithil_live_abc123");
        var hash2 = ApiKeyHasher.Hash("ithil_live_abc123");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ApiKeyHasher_ProducesDifferentHash_ForDifferentInput()
    {
        var hash1 = ApiKeyHasher.Hash("ithil_live_aaa");
        var hash2 = ApiKeyHasher.Hash("ithil_live_bbb");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ApiKeyHasher_MatchesInlineSha256_Algorithm()
    {
        // Verifies ApiKeyHasher produces the same result as the inline SHA-256
        // that was previously in ApiKeyIdentityResolver — ensures no divergence.
        const string rawKey = "ithil_live_testkey";
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)))
                              .ToLowerInvariant();

        ApiKeyHasher.Hash(rawKey).Should().Be(expected);
    }
}
