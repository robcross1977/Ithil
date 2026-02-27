namespace Ithil.SourceGenerator;

public class ToolMetadata
{
    public string MethodName { get; }
    public string Description { get; }
    public bool AllowWrite { get; }
    public int MaxResponseTokens { get; } = 2000;
    public string? Category { get; }
    public string[] RequiredScopes { get; } = [];
    public ParameterMetadata[] Parameters { get; } = [];

    public ToolMetadata(
        string methodName,
        string description,
        bool allowWrite,
        int maxResponseTokens,
        string? category,
        string[] requiredScopes,
        ParameterMetadata[] parameters)
    {
        MethodName = methodName;
        Description = description;
        AllowWrite = allowWrite;
        MaxResponseTokens = maxResponseTokens;
        Category = category;
        RequiredScopes = requiredScopes;
        Parameters = parameters;
    }
}
