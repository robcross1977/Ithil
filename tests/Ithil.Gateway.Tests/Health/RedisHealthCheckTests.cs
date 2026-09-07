using FluentAssertions;
using Ithil.Gateway.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace Ithil.Gateway.Tests.Health;

public class RedisHealthCheckTests
{
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _db = Substitute.For<IDatabase>();

    public RedisHealthCheckTests()
    {
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(_db);
    }

    [Fact]
    public async Task RedisHealthCheck_ReturnsHealthy_WhenPingSucceeds()
    {
        _db.PingAsync(Arg.Any<CommandFlags>()).Returns(TimeSpan.FromMilliseconds(1));

        var check = new RedisHealthCheck(_redis);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task RedisHealthCheck_ReturnsUnhealthy_WhenRedisThrows()
    {
        var boom = new RedisConnectionException(ConnectionFailureType.UnableToConnect, CommandFlags.None, "nope");
        _db.PingAsync(Arg.Any<CommandFlags>()).ThrowsAsync(boom);

        var check = new RedisHealthCheck(_redis);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().BeSameAs(boom);
    }
}
