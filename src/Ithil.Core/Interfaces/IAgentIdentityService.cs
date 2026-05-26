using Ithil.Core.Models;
using LanguageExt;
using Microsoft.AspNetCore.Http;

namespace Ithil.Core.Interfaces;

/// <summary>
/// Resolves an agent identity from an incoming HTTP request.
/// </summary>
public interface IAgentIdentityService
{
    /// <summary>
    /// Extracts and verifies the agent identity from the request JWT or API key.
    /// Returns None if the request carries no valid identity. 
    /// </summary>
    Task<Option<AgentIdentity>> ResolveAgentAsync(HttpContext context);
}
