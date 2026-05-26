using FluentAssertions;
using Ithil.Core;

namespace Ithil.Core.Tests;

public sealed class TraceOptionsTests
{
    [Fact]
    public void BufferSize_DefaultsTo500()
    {
        var options = new TraceOptions();

        options.BufferSize.Should().Be(500);
    }
}
