using Ithil.Core.Interfaces;

namespace Ithil.Integration.Tests.Stubs;

/// <summary>
/// Replaces the real ONNX-backed embedding service in integration tests.
/// The real implementation loads a model file from disk at startup.
/// </summary>
internal sealed class StubEmbeddingService : IEmbeddingService
{
    /// <summary>Always ready — no model file required.</summary>
    public bool IsReady => true;

    /// <summary>Returns a zero vector of the expected 384 dimensions.</summary>
    public Task<float[]> EmbedAsync(string text) =>
        Task.FromResult(new float[384]);
}
