using AwesomeAssertions;
using Ithil.Management.Audit;
using Ithil.Management.Audit.Sinks;

namespace Ithil.Management.Tests.Audit;

/// <summary>
/// Pins production defaults for audit option types.
/// </summary>
public sealed class AuditOptionsTests
{
    [Fact]
    public void AuditOptions_DisableStdoutSink_DefaultsToFalse()
    {
        // False means the stdout sink is active by default — operators must explicitly opt out.
        new AuditOptions().DisableStdoutSink.Should().BeFalse();
    }

    [Fact]
    public void FileAuditSinkOptions_FilePath_DefaultsToEmptyString()
    {
        // An empty path means the file sink must be configured before use.
        new FileAuditSinkOptions().FilePath.Should().BeEmpty();
    }
}
