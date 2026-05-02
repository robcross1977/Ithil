using System.Reflection;
using System.Security.Cryptography;
using LanguageExt;
using static LanguageExt.Prelude;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Ithil.Core.Licensing;

/// <summary>
/// Validates the RS256-signed JWT license key at startup using a public key
/// embedded in the assembly. No network call is made.
/// </summary>
public static class LicenseValidator
{
    private const string MissingMessage =
        "No Ithil license key found. Register for free at ithil.software/register.\n" +
        "Add your key to appsettings.json under Ithil:LicenseKey.";

    private const string InvalidMessage =
        "The Ithil license key is invalid or has been tampered with.\n" +
        "Register at ithil.software/register to get a new key.";

    private const string ExpiredMessage =
        "The Ithil license key has expired.\n" +
        "Non-commercial keys are valid for 30 days. Register again at ithil.software/register.";

    /// <summary>
    /// Reads and validates the license key from configuration using the embedded public key.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>A <see cref="LicenseInfo"/> containing the validated tier, email, and key ID.</returns>
    /// <exception cref="LicenseException">
    /// Thrown if the key is missing, has an invalid signature, or is missing required claims.
    /// </exception>
    public static LicenseInfo Validate(IConfiguration configuration) =>
        Validate(configuration, LoadPublicKey());

    /// <summary>
    /// Validates the license key from configuration against the provided public key.
    /// </summary>
    /// <remarks>
    /// Internal overload used by tests to supply a test key pair without touching the embedded resource.
    /// </remarks>
    internal static LicenseInfo Validate(IConfiguration configuration, RsaSecurityKey publicKey)
    {
        var token = configuration["Ithil:LicenseKey"];

        if (string.IsNullOrWhiteSpace(token))
            throw new LicenseException(MissingMessage);

        return ValidateToken(token, publicKey)
            .Match(
                Some: info => info,
                None: () => throw new LicenseException(InvalidMessage)
            );
    }

    private static RsaSecurityKey LoadPublicKey()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Ithil.Core.Licensing.license.pub")
            ?? throw new InvalidOperationException("Embedded license public key not found.");
        using var reader = new StreamReader(stream);
        var rsa = RSA.Create();
        rsa.ImportFromPem(reader.ReadToEnd());
        return new RsaSecurityKey(rsa);
    }

    private static Option<LicenseInfo> ValidateToken(string token, RsaSecurityKey publicKey)
    {
        try
        {
            var result = new JsonWebTokenHandler()
                .ValidateTokenAsync(token, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "ithil.software",
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    IssuerSigningKey = publicKey,
                    ValidAlgorithms = ["RS256"],
                })
                .GetAwaiter().GetResult();

            if (!result.IsValid)
            {
                // Throw a user-friendly message for expired keys rather than the generic invalid message.
                if (result.Exception is SecurityTokenExpiredException)
                    throw new LicenseException(ExpiredMessage);

                return None;
            }

            return ExtractLicenseInfo(result.SecurityToken as JsonWebToken);
        }
        catch (LicenseException)
        {
            throw; // let the expiry message propagate as-is
        }
        catch
        {
            return None;
        }
    }

    private static Option<LicenseInfo> ExtractLicenseInfo(JsonWebToken? jwt)
    {
        if (jwt is null) return None;

        var tier = jwt.Claims.FirstOrDefault(c => c.Type == "tier")?.Value;
        var email = jwt.Subject;
        var keyId = jwt.Id;

        return !string.IsNullOrEmpty(tier) && !string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(keyId)
            ? Some(new LicenseInfo(tier, email, keyId))
            : None;
    }
}
