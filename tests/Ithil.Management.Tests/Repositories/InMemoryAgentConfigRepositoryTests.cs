using AwesomeAssertions;
using Ithil.Core.Models;
using Ithil.Management.Repositories;
using Microsoft.Extensions.Logging;

namespace Ithil.Management.Tests.Repositories;

public class InMemoryAgentConfigRepositoryTests
{
    private readonly CapturingLogger<InMemoryAgentConfigRepository> _logger = new();

    private InMemoryAgentConfigRepository CreateRepo() => new(_logger);

    [Fact]
    public void InMemoryAgentConfigRepository_LogsWarning_OnConstruction()
    {
        _ = CreateRepo();

        _logger.Logs.Should().ContainSingle(l => l.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task InMemoryAgentConfigRepository_GetAll_ReturnsEmpty_WhenNoAgents()
    {
        var result = await CreateRepo().GetAllAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task InMemoryAgentConfigRepository_Delete_ReturnsFalse_ForUnknownAgent()
    {
        var result = await CreateRepo().DeleteAsync("does-not-exist");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task InMemoryAgentConfigRepository_Upsert_ThenGet_RoundTrips_AllFields()
    {
        var repo = CreateRepo();
        var config = BuildConfig("agent-1");

        await repo.UpsertAsync(config);
        var result = await repo.GetAsync("agent-1");

        result.IsSome.Should().BeTrue();
        var retrieved = (AgentConfig)result;
        retrieved.AgentId.Should().Be(config.AgentId);
        retrieved.Label.Should().Be(config.Label);
        retrieved.ApiKeyHash.Should().Be(config.ApiKeyHash);
        retrieved.DailyTokenBudget.Should().Be(config.DailyTokenBudget);
        retrieved.AllowedTools.Should().BeEquivalentTo(config.AllowedTools);
        retrieved.Scopes.Should().BeEquivalentTo(config.Scopes);
        retrieved.IsActive.Should().Be(config.IsActive);
    }

    [Fact]
    public async Task InMemoryAgentConfigRepository_Upsert_Overwrites_ExistingConfig()
    {
        var repo = CreateRepo();
        await repo.UpsertAsync(BuildConfig("agent-1", label: "Original"));
        await repo.UpsertAsync(BuildConfig("agent-1", label: "Updated"));

        var result = await repo.GetAsync("agent-1");

        ((AgentConfig)result).Label.Should().Be("Updated");
    }

    [Fact]
    public async Task InMemoryAgentConfigRepository_GetAll_ReturnsAllUpsertedAgents()
    {
        var repo = CreateRepo();
        await repo.UpsertAsync(BuildConfig("agent-1"));
        await repo.UpsertAsync(BuildConfig("agent-2"));
        await repo.UpsertAsync(BuildConfig("agent-3"));

        var result = await repo.GetAllAsync();

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task InMemoryAgentConfigRepository_Delete_ReturnsTrue_WhenAgentExists()
    {
        var repo = CreateRepo();
        await repo.UpsertAsync(BuildConfig("agent-1"));

        var result = await repo.DeleteAsync("agent-1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task InMemoryAgentConfigRepository_Delete_RemovesAgent_FromGetAll()
    {
        var repo = CreateRepo();
        await repo.UpsertAsync(BuildConfig("agent-1"));
        await repo.UpsertAsync(BuildConfig("agent-2"));

        await repo.DeleteAsync("agent-1");
        var result = await repo.GetAllAsync();

        result.Should().HaveCount(1);
        result.Single().AgentId.Should().Be("agent-2");
    }

    private static AgentConfig BuildConfig(string agentId, string label = "Test Agent") =>
        new()
        {
            AgentId = agentId,
            Label = label,
            ApiKeyHash = "abc123hash",
            DailyTokenBudget = 1000,
            AllowedTools = ["tool-a", "tool-b"],
            Scopes = ["read"],
            IsActive = true
        };

    // Captures log calls so tests can assert on level and message without NSubstitute's
    // known limitations around ILogger's generic Log<TState> method.
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Logs { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
            => Logs.Add((logLevel, formatter(state, exception)));
    }
}
