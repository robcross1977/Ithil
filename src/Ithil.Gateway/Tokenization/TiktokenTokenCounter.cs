using Ithil.Core.Interfaces;
using Microsoft.ML.Tokenizers;

namespace Ithil.Gateway.Tokenization;

/// <summary>
/// Counts tokens using the cl100k_base BPE tokenizer (OpenAI GPT-4/3.5).
/// Accurate for GPT-family models. For Claude traffic this is an approximation -
/// Anthropic has not published a standalone Claude tokenizer. Counts will be close
/// but should not be treated as authoritative billing figures for Claude models.
/// </summary>
internal sealed class TiktokenTokenCounter(TiktokenTokenizer tokenizer) : ITokenCounter
{
    /// <summary>
    /// Returns the BPE token count for the given text. Returns 0 for empty input.
    /// </summary>
    public int CountTokens(string text) =>
        string.IsNullOrEmpty(text) ? 0 : tokenizer.CountTokens(text);
}
