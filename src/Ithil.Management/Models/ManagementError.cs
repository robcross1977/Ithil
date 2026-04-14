namespace Ithil.Management.Models;

/// <summary>
/// Represents the ways a management operation can fail.
/// </summary>
public abstract record ManagementError
{
    /// <summary>The requested agent does not exist.</summary>
    public record NotFound(string AgentId) : ManagementError;

    /// <summary>The request contains invalid data.</summary>
    public record Invalid(string Reason) : ManagementError;
}
