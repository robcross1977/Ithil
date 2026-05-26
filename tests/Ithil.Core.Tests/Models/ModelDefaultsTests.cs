using FluentAssertions;
using Ithil.Core.Models;
using LanguageExt;

namespace Ithil.Core.Tests.Models;

/// <summary>
/// Pins the default property values for model records that carry meaningful defaults.
/// These tests exist to catch accidental changes to values that affect runtime behaviour.
/// </summary>
public sealed class ModelDefaultsTests
{
    // ── AgentConfig ──────────────────────────────────────────────────────────

    [Fact]
    public void AgentConfig_AllowedTools_DefaultsToEmptySeq()
    {
        var config = new AgentConfig { AgentId = "a", Label = "A" };

        config.AllowedTools.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void AgentConfig_Scopes_DefaultsToEmptySeq()
    {
        var config = new AgentConfig { AgentId = "a", Label = "A" };

        config.Scopes.IsEmpty.Should().BeTrue();
    }

    // ── AgentIdentity ────────────────────────────────────────────────────────

    [Fact]
    public void AgentIdentity_AllowedTools_DefaultsToEmptySeq()
    {
        var identity = new AgentIdentity { AgentId = "a", Label = "A" };

        identity.AllowedTools.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void AgentIdentity_Scopes_DefaultsToEmptySeq()
    {
        var identity = new AgentIdentity { AgentId = "a", Label = "A" };

        identity.Scopes.IsEmpty.Should().BeTrue();
    }

    // ── McpToolDefinition ────────────────────────────────────────────────────

    [Fact]
    public void McpToolDefinition_MaxResponseTokens_DefaultsTo2000()
    {
        var def = new McpToolDefinition
        {
            Name = "tool",
            Description = "does stuff",
            InputSchema = new McpInputSchema()
        };

        def.MaxResponseTokens.Should().Be(2000);
    }

    [Fact]
    public void McpToolDefinition_RequiredScopes_DefaultsToEmptyArray()
    {
        var def = new McpToolDefinition
        {
            Name = "tool",
            Description = "does stuff",
            InputSchema = new McpInputSchema()
        };

        def.RequiredScopes.Should().BeEmpty();
    }

    // ── McpInputSchema ───────────────────────────────────────────────────────

    [Fact]
    public void McpInputSchema_Properties_DefaultsToEmptyMap()
    {
        var schema = new McpInputSchema();

        schema.Properties.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void McpInputSchema_Required_DefaultsToEmptySeq()
    {
        var schema = new McpInputSchema();

        schema.Required.IsEmpty.Should().BeTrue();
    }
}
