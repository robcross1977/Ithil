using Ithil.Gateway;
using Ithil.Management.Audit.Sinks;
using Ithil.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;

namespace Ithil.Management.Tests.Audit;

public class AuditRegistrationTests
{
    [Fact]
    public void StdoutAuditSink_IsRegisteredByDefault()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        services.AddIthilAudit(config);

        var provider = services.BuildServiceProvider();
        var sinks = provider.GetServices<IAuditSink>();

        sinks.Should().ContainItemsAssignableTo<StdoutAuditSink>();
    }

    [Fact]
    public void StdoutAuditSink_IsNotRegistered_WhenDisabled()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ithil:Audit:DisableStdoutSink"] = "true"
            })
            .Build();

        services.AddIthilAudit(config);

        var provider = services.BuildServiceProvider();
        var sinks = provider.GetServices<IAuditSink>();

        sinks.Should().NotContainItemsAssignableTo<StdoutAuditSink>();
    }
}
