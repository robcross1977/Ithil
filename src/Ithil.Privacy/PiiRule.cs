using System.Text.RegularExpressions;

namespace Ithil.Privacy;

/// <summary>
/// A single PII redaction rule consisting of a pre-compiled regex and its replacement string.
/// </summary>
/// <remarks>
/// Creates a rule with the given regex pattern and replacement text.
/// </remarks>
public class PiiRule(string pattern, string replacement)
{
    private readonly Regex _regex = new(pattern, RegexOptions.Compiled);

    /// <summary>
    /// The regex pattern used to detect PII.
    /// </summary>
    public string Pattern { get; } = pattern;

    /// <summary>
    ///  The text to substitute in place of matched PII.
    /// </summary>
    public string Replacement { get; } = replacement;

    /// <summary>
    /// Replaces all matches in the given content with the replacement string.
    /// </summary>
    public string Apply(string content) => _regex.Replace(content, Replacement);
}

