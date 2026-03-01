namespace Ithil.SourceGenerator;

/// <summary>
/// Describes a single parameter extracted from an AgentTool-decorated method.
/// </summary>
public class ParameterMetadata(string name, string jsonType, string? format, bool isRequired)
{
    public string Name { get; } = name;
    public string JsonType { get; } = jsonType;
    public string? Format { get; } = format;
    public bool IsRequired { get; } = isRequired;
}
