namespace Ithil.Gateway.Auth;

/// <summary>
/// Configuration for the admin token bootstrap endpoint.
/// </summary>
public class AdminOptions
{
    /// <summary>
    /// Pre-shared key that authenticates requests to POST /auth/admin/token.
    /// Leave empty to disable the endpoint entirely.
    /// Never commit this value — inject it via an environment variable or secrets manager.
    /// Recommended: a random 64-character string.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// How many days issued admin JWTs remain valid. Defaults to 90.
    /// </summary>
    public int TokenExpiryDays { get; init; } = 90;
}
