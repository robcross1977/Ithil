namespace Ithil.Privacy;

/// <summary>
/// Configuration options for the Privacy Filter.
/// Custom rules are loaded from appsettings.json and applied after built-in rules.
/// </summary>
public class PrivacyFilterOptions
{
    /// <summary>
    /// Additional PII redaction rules defined by the operator.
    /// Applied in order, after the built-in email, SSN, and credit card rules. 
    /// </summary>
    public List<PiiRule> CustomRules { get; set; } = [];
}
