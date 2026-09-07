using AwesomeAssertions;
using Ithil.Hosting;

namespace Ithil.Hosting.Tests;

/// <summary>
/// Pins the default values on ToolEntry.
/// ToolEntry instances are emitted by the source generator, which always sets every property
/// explicitly. These defaults exist for defensive construction but pinning them prevents
/// silent regressions if a default changes between generator runs.
/// </summary>
public sealed class ToolEntryDefaultsTests
{
    [Fact]
    public void Name_DefaultsToEmptyString()
    {
        new ToolEntry().Name.Should().BeEmpty();
    }

    [Fact]
    public void Description_DefaultsToEmptyString()
    {
        new ToolEntry().Description.Should().BeEmpty();
    }

    [Fact]
    public void AllowWrite_DefaultsToFalse()
    {
        new ToolEntry().AllowWrite.Should().BeFalse();
    }

    [Fact]
    public void MaxResponseTokens_DefaultsToZero()
    {
        // The source generator always emits this value from [AgentTool(MaxResponseTokens = N)],
        // so 0 is never seen in practice — but pinned here to catch unintentional changes.
        new ToolEntry().MaxResponseTokens.Should().Be(0);
    }

    [Fact]
    public void Category_DefaultsToNull()
    {
        new ToolEntry().Category.Should().BeNull();
    }

    [Fact]
    public void RequiredScopes_DefaultsToEmptyArray()
    {
        new ToolEntry().RequiredScopes.Should().BeEmpty();
    }

    [Fact]
    public void HttpMethod_DefaultsToEmptyString()
    {
        new ToolEntry().HttpMethod.Should().BeEmpty();
    }

    [Fact]
    public void RoutePattern_DefaultsToEmptyString()
    {
        new ToolEntry().RoutePattern.Should().BeEmpty();
    }

    [Fact]
    public void ParameterSources_DefaultsToEmptyDictionary()
    {
        new ToolEntry().ParameterSources.Should().BeEmpty();
    }

    [Fact]
    public void ParameterTypes_DefaultsToEmptyDictionary()
    {
        new ToolEntry().ParameterTypes.Should().BeEmpty();
    }
}
