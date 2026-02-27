namespace Ithil.SourceGenerator;

/// <summary>
/// Describes a single parameter extracted from an AgentTool-decorated method.
/// </summary>
public class ParameterMetadata
{
    public string Name { get; }
    public string JsonType { get; }
    public string? Format { get; }
    public bool IsRequired { get; }

    public ParameterMetadata(string name, string jsonType, string? format, bool isRequired)
    {
        Name = name;
        JsonType = jsonType;
        Format = format;
        IsRequired = isRequired;
    }
}
