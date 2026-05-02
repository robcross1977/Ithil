using Ithil.Core.Interfaces;

namespace Ithil.Integration.Tests.Stubs;

/// <summary>
/// Replaces the real tiktoken-backed counter in integration tests.
/// The real implementation downloads a vocabulary file from the internet at startup.
/// </summary>
internal sealed class StubTokenCounter : ITokenCounter
{
    /// <summary>Returns a fixed count of 100 for every input.</summary>
    public int CountTokens(string text) => 100;
}
