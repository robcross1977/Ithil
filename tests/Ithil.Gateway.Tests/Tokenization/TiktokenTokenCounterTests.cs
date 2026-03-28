using FluentAssertions;
using Ithil.Gateway.Tokenization;
using Microsoft.ML.Tokenizers;

namespace Ithil.Gateway.Tests.Tokenization;

public class TiktokenTokenCounterTests
{
    private static TiktokenTokenCounter CreateCounter()
    {
        using var http = new HttpClient();
        using var vocabStream = http.GetStreamAsync(
            "https://openaipublic.blob.core.windows.net/encodings/cl100k_base.tiktoken"
        ).GetAwaiter().GetResult();
        var tokenizer = TiktokenTokenizer.CreateForModelAsync("gpt-4", vocabStream)
            .GetAwaiter().GetResult();
        return new TiktokenTokenCounter(tokenizer);
    }

    [Fact]
    public void CountTokens_ReturnsZero_ForEmptyString()
    {
        var counter = CreateCounter();
        counter.CountTokens("").Should().Be(0);
    }

    [Fact]
    public void CountTokens_ReturnsExpectedCount_ForKnownEnglishSentence()
    {
        var counter = CreateCounter();
        // "Hello, world!" is 4 tokens in cl100k_base: ["Hello", ",", " world", "!"]
        counter.CountTokens("Hello, world!").Should().Be(4);
    }
}