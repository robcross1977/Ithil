namespace Ithil.Management.Models;

/// <summary>
/// Optional body for POST /management/agents/{id}/token.
/// Omit entirely to accept the default 30-day expiry.
/// </summary>
public record IssueTokenRequest
{
    /// <summary>Number of days until the issued token expires. Defaults to 30.</summary>
    public int ExpiresInDays { get; init; } = 30;
}
