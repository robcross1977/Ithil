using AwesomeAssertions;
using Ithil.Gateway.Mcp;

namespace Ithil.Gateway.Tests.Mcp;

/// <summary>
/// Pins defaults and the computed SchemaUrl property for ToolRegistryOptions.
/// SchemaUrl is derived (not stored), so we verify both the default path value
/// and that the computation correctly joins base URL and schema path.
/// </summary>
public sealed class ToolRegistryOptionsTests
{
    [Fact]
    public void DownstreamBaseUrl_DefaultsToEmptyString()
    {
        new ToolRegistryOptions().DownstreamBaseUrl.Should().BeEmpty();
    }

    [Fact]
    public void SchemaPath_DefaultsToExpectedPath()
    {
        new ToolRegistryOptions().SchemaPath.Should().Be("/ithil/schema");
    }

    [Fact]
    public void SchemaUrl_CombinesBaseUrlAndPath()
    {
        var options = new ToolRegistryOptions
        {
            DownstreamBaseUrl = "http://localhost:5200",
            SchemaPath = "/ithil/schema"
        };

        options.SchemaUrl.Should().Be("http://localhost:5200/ithil/schema");
    }

    [Fact]
    public void SchemaUrl_TrimsTrailingSlashFromBaseUrl()
    {
        // Without trimming, the URL would be "http://localhost:5200//ithil/schema"
        var options = new ToolRegistryOptions
        {
            DownstreamBaseUrl = "http://localhost:5200/",
            SchemaPath = "/ithil/schema"
        };

        options.SchemaUrl.Should().Be("http://localhost:5200/ithil/schema");
    }
}
