using Ithil.Core.Interfaces;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace Ithil.Cache;

/// <summary>
/// Generates 384-dimensional semantic embedding vectors from text using the
/// all-MiniLM-L6-v2 ONNX model running locally. No data leaves the network.
///
/// Pipeline per call:
///   text → BERT tokenizer → int64 tensors → ONNX inference → mean pool → L2 normalize → float[384]
/// </summary>
public class EmbeddingService : IEmbeddingService, IDisposable
{
    // InferenceSession loads and JIT-compiles the ONNX model on construction.
    // This is expensive (~200ms) so we do it once and reuse across requests.
    // InferenceSession.Run() is thread-safe — safe to share across concurrent calls.
    private readonly InferenceSession _session;

    // BERT tokenizer: splits text into subword units from a fixed vocabulary.
    // Example: "embedding" → ["em", "##bed", "##ding"] → [7861, 8270, 4667]
    // The ## prefix means "continuation of previous word" — not a new token.
    private readonly BertTokenizer _tokenizer;

    // Set true once both the ONNX session and tokenizer have loaded successfully.
    // If either throws during construction, DI fails and this instance never exists —
    // so in practice this is always true for a live instance, but keeping it explicit
    // documents the contract for IEmbeddingService.IsReady.
    private readonly bool _isReady;

    // all-MiniLM-L6-v2 was trained with sequences up to 256 tokens.
    // Longer inputs are truncated — the beginning is kept, the end is dropped.
    private const int MaxTokens = 256;

    // The output dimension of all-MiniLM-L6-v2. Each token gets a 384-float vector.
    private const int EmbeddingDims = 384;

    public EmbeddingService(SemanticCacheOptions options)
    {
        _session = new InferenceSession(options.ModelPath);
        // doLowerCase: true normalises casing so "Widget" and "widget" embed identically.
        _tokenizer = BertTokenizer.Create(options.VocabPath, new BertOptions { LowerCaseBeforeTokenization = true });
        _isReady = true;
    }

    /// <inheritdoc/>
    public bool IsReady => _isReady;

    /// <summary>
    /// Converts text into a unit-length float[384] vector representing its semantic meaning.
    /// Runs synchronously on the CPU — offloaded to a thread pool thread to avoid
    /// blocking the ASP.NET request thread during inference.
    /// </summary>
    public Task<float[]> EmbedAsync(string text) =>
        Task.Run(() => Embed(text));

    private float[] Embed(string text)
    {
        // Step 1: Tokenize. BERT requires [CLS] at the start and [SEP] at the end.
        // [CLS] = token 101, [SEP] = token 102 in the standard BERT vocabulary.
        // EncodeToIds adds these special tokens automatically.
        var tokenIds = _tokenizer.EncodeToIds(text, considerPreTokenization: true, considerNormalization: true);
        var seqLen = Math.Min(tokenIds.Count, MaxTokens);

        // Step 2: Build the three input tensors the model expects.
        // All are int64 with shape [1, seqLen] (batch size of 1).
        var inputIds      = new long[seqLen];
        var attentionMask = new long[seqLen];
        var tokenTypeIds  = new long[seqLen]; // all zeros — only used for two-sentence tasks

        for (var i = 0; i < seqLen; i++)
        {
            inputIds[i]      = tokenIds[i];
            attentionMask[i] = 1L; // 1 = real token, 0 = padding (no padding here)
        }

        // Wrap in DenseTensor with shape [batch=1, sequence=seqLen].
        var inputIdsTensor      = new DenseTensor<long>(inputIds,      [1, seqLen]);
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, [1, seqLen]);
        var tokenTypeIdsTensor  = new DenseTensor<long>(tokenTypeIds,  [1, seqLen]);

        // Step 3: Run inference. Input/output names must match the ONNX model's nodes.
        // For all-MiniLM-L6-v2 the output node is "last_hidden_state".
        using var results = _session.Run(
        [
            NamedOnnxValue.CreateFromTensor("input_ids",      inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
        ]);

        // last_hidden_state shape: [1, seqLen, 384]
        // Each position t contains a 384-float vector for that token.
        var hiddenState = results[0].AsTensor<float>();

        // Step 4: Mean pooling — collapse the seqLen token vectors into one.
        // We average only the real tokens (where attentionMask[t] == 1).
        var pooled = MeanPool(hiddenState, attentionMask, seqLen);

        // Step 5: L2 normalize — scale to unit length so cosine similarity
        // equals the dot product. Required for Redis vector search.
        return L2Normalize(pooled);
    }

    /// <summary>
    /// Averages all token vectors, weighted by the attention mask.
    /// Padding tokens (mask=0) are excluded so they don't dilute the result.
    /// Output: float[384]
    /// </summary>
    private static float[] MeanPool(Tensor<float> hiddenState, long[] attentionMask, int seqLen)
    {
        var pooled = new float[EmbeddingDims];
        var maskSum = 0L;

        for (var t = 0; t < seqLen; t++)
        {
            if (attentionMask[t] == 0) continue;
            maskSum++;
            for (var d = 0; d < EmbeddingDims; d++)
                pooled[d] += hiddenState[0, t, d];
        }

        if (maskSum == 0) return pooled;

        for (var d = 0; d < EmbeddingDims; d++)
            pooled[d] /= maskSum;

        return pooled;
    }

    /// <summary>
    /// Scales a vector to unit length (L2 norm = 1.0).
    /// Without normalisation, dot product is affected by vector magnitude.
    /// With normalisation, dot product == cosine similarity, which is what Redis uses.
    /// </summary>
    private static float[] L2Normalize(float[] vector)
    {
        var magnitude = MathF.Sqrt(vector.Sum(x => x * x));
        if (magnitude == 0f) return vector;
        return [.. vector.Select(x => x / magnitude)];
    }

    public void Dispose()
    {
        _session.Dispose();
        GC.SuppressFinalize(this);
    }
}
