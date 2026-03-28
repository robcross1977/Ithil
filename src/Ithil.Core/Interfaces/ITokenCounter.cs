namespace Ithil.Core.Interfaces;

/// <summary>
/// Counts the number of tokens in a text string using a BPE tokenizer.
/// </summary>
public interface ITokenCounter
{
    /// <summary>
    /// Returns the token count for the given text. 
    /// </summary>
    /// <param name="text">The text to tokenize.</param>
    /// <returns>The number of tokens in the text.</returns>
    int CountTokens(string text);
}