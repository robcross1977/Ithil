namespace Ithil.Core.Licensing;

/// <summary>
/// Holds the validated claims extracted from a license key at startup.
/// </summary>
/// <param name="Tier">The license tier: <c>non-commercial</c> or <c>commercial</c>.</param>
/// <param name="Email">The registrant's email address.</param>
/// <param name="KeyId">The unique key identifier (<c>jti</c> claim).</param>
public record LicenseInfo(string Tier, string Email, string KeyId);
