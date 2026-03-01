using Ithil.Core.Interfaces;
using System.Text;
using System.Text.RegularExpressions;

namespace Ithil.Privacy;

/// <summary>
/// Scrubs PII from response bodies before they are returned to the calling agent.
/// Built-in rules cover emails, SSNs, and credit card numbers.
/// Additional rules can be configured via PrivacyFilterOptions.
/// </summary>
public class PrivacyFilterService(PrivacyFilterOptions options)
{
    private readonly PrivacyFilterOptions _options = options;

    // Pre-compiled at startup — not recompiled per request.
    private static readonly Regex EmailRegex = new(
        @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled);

    private static readonly Regex SsnRegex = new(
        @"\b\d{3}-\d{2}-\d{4}\b",
        RegexOptions.Compiled);

    private static readonly Regex CreditCardRegex = new(
        @"\b(?:\d[ -]?){13,15}\d\b",
        RegexOptions.Compiled);

    /// <summary>
    /// Reads the response body stream, applies all PII redaction rules, and returns the scrubbed string.
    /// </summary>
    public async Task<string> ScrubAsync(Stream responseBody)
    {
        using var reader = new StreamReader(responseBody, Encoding.UTF8, leaveOpen: true);
        var content = await reader.ReadToEndAsync();

        if (string.IsNullOrEmpty(content)) return string.Empty;

        content = EmailRegex.Replace(content, "[EMAIL REDACTED]");
        content = SsnRegex.Replace(content, "[SSN REDACTED]");
        content = CreditCardRegex.Replace(content, "[CARD REDACTED]");

        foreach (var rule in _options.CustomRules)
            content = rule.Apply(content);

        return content;
    }
}
