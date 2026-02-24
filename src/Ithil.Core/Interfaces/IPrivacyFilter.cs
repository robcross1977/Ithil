namespace Ithil.Core.Interfaces;

/// <summary>
/// Scrubs PII from response bodies before they are returned to agents.
/// </summary>
public interface IPrivacyFilter
{
    /// <summary>
    /// Reads the body stream, redacts PII, and returns the cleaned string.
    /// </summary>
    Task<string> ScrubAsync(Stream body);
}
