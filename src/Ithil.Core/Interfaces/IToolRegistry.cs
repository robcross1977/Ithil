using Ithil.Core.Models;
using LanguageExt;

namespace Ithil.Core.Interfaces;

/// <summary>
/// Provides access to the tool definitions exposed by the downstream service.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Returns all registered tools, fetching from the downstream schema endpoint if not yet cached.
    /// </summary>
    Task<Seq<ToolRegistryEntry>> GetToolsAsync(CancellationToken cancellationToken = default);
}
