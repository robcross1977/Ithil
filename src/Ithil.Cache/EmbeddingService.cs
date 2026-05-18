using Ithil.Core.Interfaces;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace Ithil.Cache;

/// <summary>
/// Turns text into a list of 384 numbers that represents its meaning — an "embedding vector".
/// Similar phrases produce similar vectors, so they can be compared mathematically.
/// The model runs locally on the CPU; no text leaves the network.
///
/// Pipeline per call:
///   text
///     → tokenizer     (split text into numbered word fragments)
///     → int64 tensors (package those numbers in the format the model expects)
///     → ONNX model    (run the neural network to get one meaning-vector per fragment)
///     → mean pool     (average the per-fragment vectors into one vector for the whole text)
///     → L2 normalize  (scale the vector to unit length so similarity comparisons are consistent)
///     → float[384]    (the final embedding — 384 numbers representing the text's meaning)
/// </summary>
public class EmbeddingService : IEmbeddingService, IDisposable
{
    // An InferenceSession is the loaded, ready-to-run form of an ONNX model.
    // ONNX (Open Neural Network Exchange) is a file format for storing trained neural networks —
    // like a compiled binary you can run without the original training framework.
    // Loading and JIT-compiling the model is expensive (~200ms) so we do it once and reuse.
    // InferenceSession.Run() is thread-safe — safe to share across concurrent calls.
    private readonly InferenceSession _session;

    // The BERT tokenizer converts raw text into a sequence of integer IDs.
    // BERT (Bidirectional Encoder Representations from Transformers) is the model family
    // this tokenizer was designed for. It uses a fixed vocabulary of ~30,000 subword fragments,
    // so rare words are split into smaller known pieces rather than being treated as unknown.
    // Example: "embedding" → ["em", "##bed", "##ding"] → [7861, 8270, 4667]
    // The ## prefix means "continuation of previous word" — not the start of a new token.
    private readonly BertTokenizer _tokenizer;

    // Set true once both the ONNX session and tokenizer have loaded successfully.
    // If either throws during construction, DI fails and this instance never exists —
    // so in practice this is always true for a live instance, but keeping it explicit
    // documents the contract for IEmbeddingService.IsReady.
    private readonly bool _isReady;

    // all-MiniLM-L6-v2 was trained with sequences up to 256 tokens.
    // Longer inputs are truncated — the beginning is kept, the end is dropped.
    private const int MaxTokens = 256;

    // The number of dimensions in each output embedding vector.
    // A "dimension" here is just one number in the list of 384. The model was trained to
    // always output exactly 384 numbers — this is a design choice baked into the model,
    // like how a colour is always represented with exactly 3 numbers (RGB).
    private const int EmbeddingDims = 384;

    public EmbeddingService(SemanticCacheOptions options)
    {
        _session = new InferenceSession(options.ModelPath);
        // LowerCaseBeforeTokenization: true normalises casing so "Widget" and "widget"
        // produce the same token IDs and therefore the same embedding.
        _tokenizer = BertTokenizer.Create(options.VocabPath, new BertOptions { LowerCaseBeforeTokenization = true });
        _isReady = true;
    }

    /// <inheritdoc/>
    public bool IsReady => _isReady;

    /// <summary>
    /// Converts text into an embedding — a float[384] vector representing its semantic meaning.
    /// The model runs synchronously on the CPU, so this is offloaded to a thread pool thread
    /// to avoid blocking the ASP.NET request thread during inference.
    /// </summary>
    public Task<float[]> EmbedAsync(string text) =>
        Task.Run(() => Embed(text));

    private float[] Embed(string text)
    {
        // Step 1: Tokenize — convert the raw text into a sequence of integer token IDs.
        // BERT requires a special [CLS] ("classify") token at the start and a [SEP] ("separator")
        // token at the end of every input. These are token IDs 101 and 102 in the standard
        // BERT vocabulary. EncodeToIds adds them automatically.
        var tokenIds = _tokenizer.EncodeToIds(text, considerPreTokenization: true, considerNormalization: true);
        var seqLen = Math.Min(tokenIds.Count, MaxTokens);

        // Step 2: Build the three input arrays the model expects.
        //
        // A "tensor" is just a multi-dimensional array. "int64" means each cell holds a
        // 64-bit integer — the same as `long` in C#. "Shape [1, seqLen]" means a 2D grid:
        // 1 row (we always process one sentence at a time — a "batch size" of 1) and
        // seqLen columns (one column per token).
        //
        // The model always expects these three named inputs:
        var inputIds      = new long[seqLen]; // the token ID numbers from the tokenizer
        var attentionMask = new long[seqLen]; // 1 for real tokens, 0 for padding (no padding here)
        var tokenTypeIds  = new long[seqLen]; // which sentence each token belongs to — always 0
                                              // (BERT supports two-sentence inputs like "given
                                              // sentence A and B, are they related?"; we only
                                              // ever pass one sentence so this is always all zeros)

        for (var i = 0; i < seqLen; i++)
        {
            inputIds[i]      = tokenIds[i];
            attentionMask[i] = 1L; // all tokens are real — we never pad short inputs
        }

        // Wrap the flat arrays in DenseTensor objects with explicit shape [batch=1, sequence=seqLen].
        // The ONNX model was saved expecting tensors with named shapes, not plain arrays.
        var inputIdsTensor      = new DenseTensor<long>(inputIds,      [1, seqLen]);
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, [1, seqLen]);
        var tokenTypeIdsTensor  = new DenseTensor<long>(tokenTypeIds,  [1, seqLen]);

        // Step 3: Run the model ("inference" — using the trained model to produce output,
        // as opposed to training it). Each input tensor must be labelled with the name the
        // model's input slot expects — these names are baked into the ONNX file.
        using var results = _session.Run(
        [
            NamedOnnxValue.CreateFromTensor("input_ids",      inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
        ]);

        // The model outputs one 384-number vector per input token — these are called
        // "hidden states" (a historical term for the internal representations a neural
        // network builds for each position in the input). The output tensor has shape
        // [1, seqLen, 384]: one batch × seqLen tokens × 384 numbers per token.
        var hiddenState = results[0].AsTensor<float>();

        // Step 4: Mean pooling — collapse the seqLen per-token vectors into one.
        // For example, "what is the stock level?" produces 9 token vectors. Mean pooling
        // averages them into a single float[384] that represents the meaning of the
        // whole phrase, not just individual words.
        // Only real tokens (attentionMask == 1) contribute to the average.
        var pooled = MeanPool(hiddenState, attentionMask, seqLen);

        // Step 5: L2 normalize — scale the vector to "unit length" (magnitude = 1.0).
        // This is required for consistent similarity comparisons — see L2Normalize below.
        return L2Normalize(pooled);
    }

    /// <summary>
    /// Averages the per-token hidden state vectors into a single float[384] embedding
    /// representing the whole input. Only tokens where attentionMask == 1 are included —
    /// masked (padding) tokens are excluded so they don't dilute the result.
    /// </summary>
    internal static float[] MeanPool(Tensor<float> hiddenState, long[] attentionMask, int seqLen)
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
    /// Scales a vector to unit length — a "magnitude" (overall size) of exactly 1.0.
    /// Think of it as taking an arrow pointing in some direction and resizing it to be
    /// exactly 1 unit long. The direction (meaning) is unchanged; only the length changes.
    ///
    /// This is called "L2 normalization" (L2 = the standard straight-line distance formula,
    /// also known as Euclidean distance — sqrt of sum of squares, which is Pythagoras
    /// generalised to 384 dimensions instead of 2).
    ///
    /// We do this because Redis finds similar vectors by measuring the angle between them —
    /// "cosine similarity": 1.0 means identical direction, 0.0 means completely unrelated.
    /// When all vectors are already unit length, that angle calculation simplifies to a plain
    /// dot product (multiply matching numbers together and sum them), which is what Redis
    /// actually computes. Without normalization, two identical phrases that happened to produce
    /// slightly different-magnitude vectors would look less similar than they really are.
    /// </summary>
    internal static float[] L2Normalize(float[] vector)
    {
        // Magnitude = sqrt(sum of squares) — the same formula as the length of a line segment
        // in 2D geometry (sqrt(x² + y²)), just extended to 384 dimensions.
        var magnitude = MathF.Sqrt(vector.Sum(x => x * x));
        if (magnitude == 0f) return vector; // zero vector has no direction — return as-is
        return [.. vector.Select(x => x / magnitude)];
    }

    public void Dispose()
    {
        _session.Dispose();
        GC.SuppressFinalize(this);
    }
}
