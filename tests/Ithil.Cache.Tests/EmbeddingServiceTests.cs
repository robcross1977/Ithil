using FluentAssertions;
using Ithil.Cache;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Ithil.Cache.Tests;

/// <summary>
/// Tests for the internal pure-math helpers in EmbeddingService.
/// The full EmbedAsync pipeline requires real ONNX model files on disk so cannot
/// be unit tested here — it is exercised via integration tests using StubEmbeddingService.
/// </summary>
public class EmbeddingServiceTests
{
    // -------------------------------------------------------------------------
    // L2Normalize
    // -------------------------------------------------------------------------

    [Fact]
    public void L2Normalize_ZeroVector_ReturnsZeroVector()
    {
        // A zero-magnitude vector would cause division by zero.
        // The guard returns the vector unchanged instead.
        var result = EmbeddingService.L2Normalize(new float[4]);

        result.Should().AllBeEquivalentTo(0f);
    }

    [Fact]
    public void L2Normalize_KnownVector_ProducesCorrectUnitVector()
    {
        // [3, 4] has magnitude 5. Normalised: [0.6, 0.8].
        var result = EmbeddingService.L2Normalize([3f, 4f]);

        result[0].Should().BeApproximately(0.6f, 1e-6f);
        result[1].Should().BeApproximately(0.8f, 1e-6f);
    }

    [Fact]
    public void L2Normalize_Output_HasMagnitudeOfOne()
    {
        // For any non-zero input the output vector must be unit length.
        var input = Enumerable.Range(1, 10).Select(i => (float)i).ToArray();

        var result = EmbeddingService.L2Normalize(input);

        var magnitude = MathF.Sqrt(result.Sum(x => x * x));
        magnitude.Should().BeApproximately(1f, 1e-6f);
    }

    [Fact]
    public void L2Normalize_AlreadyNormalized_RemainsUnchanged()
    {
        // [1, 0, 0] already has magnitude 1 — the operation is a no-op.
        var result = EmbeddingService.L2Normalize([1f, 0f, 0f]);

        result[0].Should().BeApproximately(1f, 1e-6f);
        result[1].Should().BeApproximately(0f, 1e-6f);
        result[2].Should().BeApproximately(0f, 1e-6f);
    }

    // -------------------------------------------------------------------------
    // MeanPool
    // -------------------------------------------------------------------------

    [Fact]
    public void MeanPool_SingleToken_AllMasked_ReturnsTokenValues()
    {
        // One token, all dims = 2.0f, mask = 1. Mean of one value is itself.
        const int seqLen = 1;
        var hiddenState = new DenseTensor<float>([1, seqLen, 384]);
        for (var d = 0; d < 384; d++)
            hiddenState[0, 0, d] = 2.0f;
        var mask = new long[] { 1 };

        var result = EmbeddingService.MeanPool(hiddenState, mask, seqLen);

        result.Length.Should().Be(384);
        result[0].Should().BeApproximately(2.0f, 1e-6f);
    }

    [Fact]
    public void MeanPool_TwoTokens_BothMasked_AveragesValues()
    {
        // Token 0 = 4.0f, token 1 = 2.0f, both active. Mean = 3.0f.
        const int seqLen = 2;
        var hiddenState = new DenseTensor<float>([1, seqLen, 384]);
        for (var d = 0; d < 384; d++)
        {
            hiddenState[0, 0, d] = 4.0f;
            hiddenState[0, 1, d] = 2.0f;
        }
        var mask = new long[] { 1, 1 };

        var result = EmbeddingService.MeanPool(hiddenState, mask, seqLen);

        result[0].Should().BeApproximately(3.0f, 1e-6f);
    }

    [Fact]
    public void MeanPool_SecondTokenMaskedOut_OnlyFirstTokenContributes()
    {
        // Token 1 has mask=0 so its large value must not influence the output.
        const int seqLen = 2;
        var hiddenState = new DenseTensor<float>([1, seqLen, 384]);
        for (var d = 0; d < 384; d++)
        {
            hiddenState[0, 0, d] = 4.0f;
            hiddenState[0, 1, d] = 999.0f; // masked out — must be excluded
        }
        var mask = new long[] { 1, 0 };

        var result = EmbeddingService.MeanPool(hiddenState, mask, seqLen);

        result[0].Should().BeApproximately(4.0f, 1e-6f);
    }

    [Fact]
    public void MeanPool_AllMaskZero_ReturnsZeroVector()
    {
        // maskSum == 0 triggers the early-return guard — output is the zero vector.
        const int seqLen = 1;
        var hiddenState = new DenseTensor<float>([1, seqLen, 384]);
        for (var d = 0; d < 384; d++)
            hiddenState[0, 0, d] = 99.0f;
        var mask = new long[] { 0 };

        var result = EmbeddingService.MeanPool(hiddenState, mask, seqLen);

        result.Should().AllBeEquivalentTo(0f);
    }
}
