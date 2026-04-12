using FluentAssertions;
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
