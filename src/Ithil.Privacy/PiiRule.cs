using System.Text.RegularExpressions;

namespace Ithil.Privacy;

/// <summary>
/// A single PII redation rule consisting of a pre-compiled regex and its replacement string.
/// </summary>
public class PiiRule
{
    private readonly Regex _regex;

    /// <summary>
    /// Creates a rule with the given regex pattern and replacement text.
    /// </summary>
    public PiiRule(string pattern, string replacement)
    {
        Pattern = pattern;
        Replacement = replacement;
        _regex = new Regex(pattern, RegexOptions.Compiled);
    }

    /// <summary>
    /// The regex pattern used to detect PII.
    /// </summary>
    public string Pattern { get; }

    /// <summary>
    ///  The text to substitute in place of matched PII.
    /// </summary>
    public string Replacement { get; }

    /// <summary>
    /// Replaces all matches in the given content with the replacement string.
    /// </summary>
    public string Apply(string content) => _regex.Replace(content, Replacement);
}

