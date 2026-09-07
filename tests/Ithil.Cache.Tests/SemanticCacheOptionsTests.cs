using AwesomeAssertions;
using Ithil.Core.Enums;

namespace Ithil.Cache.Tests;

public class SemanticCacheOptionsTests
{
    [Fact]
    public void SimilarityThreshold_DefaultsTo0Point95()
    {
        var options = new SemanticCacheOptions();

        options.SimilarityThreshold.Should().BeApproximately(0.95f, 1e-6f);
    }

    [Fact]
    public void DefaultTtl_DefaultsTo15Minutes()
    {
        var options = new SemanticCacheOptions();

        options.DefaultTtl.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void ModelPath_DefaultsToExpectedPath()
    {
        var options = new SemanticCacheOptions();

        options.ModelPath.Should().Be("models/all-MiniLM-L6-v2.onnx");
    }

    [Fact]
    public void VocabPath_DefaultsToExpectedPath()
    {
        var options = new SemanticCacheOptions();

        options.VocabPath.Should().Be("models/vocab.txt");
    }

    [Fact]
    public void FailurePolicy_DefaultsToFailOpen()
    {
        var options = new SemanticCacheOptions();

        options.FailurePolicy.Should().Be(RedisFailurePolicy.FailOpen);
    }
}
