using System.Security.Cryptography;
using AwesomeAssertions;
using Ithil.Core.Licensing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Ithil.Core.Tests.Licensing;

public sealed class LicenseValidatorTests : IDisposable
{
    private readonly RSA _privateKey = RSA.Create(2048);
    private readonly RsaSecurityKey _publicKey;

    public LicenseValidatorTests()
    {
        _publicKey = new RsaSecurityKey(_privateKey);
    }

    public void Dispose() => _privateKey.Dispose();

    [Fact]
    public void Validate_ValidNonCommercialJwt_ReturnsCorrectLicenseInfo()
    {
        var token = BuildToken("user@example.com", "non-commercial");
        var config = BuildConfig(token);

        var result = LicenseValidator.Validate(config, _publicKey);

        result.Tier.Should().Be("non-commercial");
        result.Email.Should().Be("user@example.com");
        result.KeyId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Validate_ValidCommercialJwt_ReturnsCorrectLicenseInfo()
    {
        var token = BuildToken("corp@example.com", "commercial");
        var config = BuildConfig(token);

        var result = LicenseValidator.Validate(config, _publicKey);

        result.Tier.Should().Be("commercial");
        result.Email.Should().Be("corp@example.com");
    }

    [Fact]
    public void Validate_MissingKeyInConfig_ThrowsLicenseExceptionWithUrl()
    {
        var config = BuildConfig(null);

        var act = () => LicenseValidator.Validate(config, _publicKey);

        act.Should().Throw<LicenseException>()
            .WithMessage("*ithil.software*");
    }

    [Fact]
    public void Validate_EmptyStringInConfig_ThrowsLicenseException()
    {
        var config = BuildConfig(string.Empty);

        var act = () => LicenseValidator.Validate(config, _publicKey);

        act.Should().Throw<LicenseException>();
    }

    [Fact]
    public void Validate_JwtSignedWithDifferentKey_ThrowsLicenseException()
    {
        using var otherKey = RSA.Create(2048);
        var token = BuildToken("user@example.com", "non-commercial", signingKey: otherKey);
        var config = BuildConfig(token);

        var act = () => LicenseValidator.Validate(config, _publicKey);

        act.Should().Throw<LicenseException>();
    }

    [Fact]
    public void Validate_TamperedPayload_ThrowsLicenseException()
    {
        var token = BuildToken("user@example.com", "non-commercial");
        var parts = token.Split('.');

        // Replace payload with a different base64 blob without re-signing
        var tamperedPayload = Convert.ToBase64String("{\"sub\":\"hacker@evil.com\",\"tier\":\"commercial\"}"u8.ToArray());
        var tampered = $"{parts[0]}.{tamperedPayload}.{parts[2]}";
        var config = BuildConfig(tampered);

        var act = () => LicenseValidator.Validate(config, _publicKey);

        act.Should().Throw<LicenseException>();
    }

    [Fact]
    public void Validate_JwtMissingTierClaim_ThrowsLicenseException()
    {
        var token = BuildToken("user@example.com", tier: null);
        var config = BuildConfig(token);

        var act = () => LicenseValidator.Validate(config, _publicKey);

        act.Should().Throw<LicenseException>();
    }

    [Fact]
    public void Validate_JwtMissingSubClaim_ThrowsLicenseException()
    {
        var token = BuildToken(email: null, "non-commercial");
        var config = BuildConfig(token);

        var act = () => LicenseValidator.Validate(config, _publicKey);

        act.Should().Throw<LicenseException>();
    }

    [Fact]
    public void Validate_JwtMissingJtiClaim_ThrowsLicenseException()
    {
        var token = BuildToken("user@example.com", "non-commercial", includeJti: false);
        var config = BuildConfig(token);

        var act = () => LicenseValidator.Validate(config, _publicKey);

        act.Should().Throw<LicenseException>();
    }

    [Fact]
    public void Validate_JwtWithWrongIssuer_ThrowsLicenseException()
    {
        var token = BuildToken("user@example.com", "non-commercial", issuer: "evil.software");
        var config = BuildConfig(token);

        var act = () => LicenseValidator.Validate(config, _publicKey);

        act.Should().Throw<LicenseException>();
    }

    [Fact]
    public void Validate_ExpiredJwt_ThrowsLicenseExceptionWithExpiredMessage()
    {
        var token = BuildToken("user@example.com", "non-commercial",
            expires: DateTimeOffset.UtcNow.AddDays(-1));
        var config = BuildConfig(token);

        var act = () => LicenseValidator.Validate(config, _publicKey);

        act.Should().Throw<LicenseException>()
            .WithMessage("*expired*");
    }

    // --- helpers ---

    private string BuildToken(
        string? email,
        string? tier,
        RSA? signingKey = null,
        string issuer = "ithil.software",
        bool includeJti = true,
        DateTimeOffset? expires = null)
    {
        var key = new RsaSecurityKey(signingKey ?? _privateKey);
        var claims = new Dictionary<string, object> { ["iss"] = issuer };

        if (email is not null) claims["sub"] = email;
        if (tier is not null) claims["tier"] = tier;
        if (includeJti) claims["jti"] = Guid.NewGuid().ToString();

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Claims = claims,
            // When building an expired token, set NotBefore/IssuedAt in the past too so the
            // token has a structurally consistent lifetime (issued → notBefore < expires).
            NotBefore = expires.HasValue ? expires.Value.AddHours(-1).UtcDateTime : (DateTime?)null,
            IssuedAt  = expires.HasValue ? expires.Value.AddHours(-1).UtcDateTime : (DateTime?)null,
            Expires   = expires?.UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
        });
    }

    private static IConfiguration BuildConfig(string? licenseKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(licenseKey is not null
                ? [new KeyValuePair<string, string?>("Ithil:LicenseKey", licenseKey)]
                : [])
            .Build();
}
