namespace Ithil.Core.Licensing;

/// <summary>
/// Thrown at startup when the Ithil license key is missing or invalid.
/// Carries a user-facing message that includes the registration URL.
/// </summary>
public sealed class LicenseException(string message) : Exception(message);
