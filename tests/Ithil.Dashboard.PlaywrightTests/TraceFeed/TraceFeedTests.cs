using FluentAssertions;
using Ithil.Core.Models;
using Ithil.Dashboard.PlaywrightTests.Fixtures;
using Microsoft.Playwright;

namespace Ithil.Dashboard.PlaywrightTests.TraceFeed;

/// <summary>
/// Playwright tests for the Live Trace Feed dashboard page.
/// Covers redirect enforcement, history replay on load, live event delivery,
/// per-agent filtering, filter clearing, and unidentified traffic routing.
/// </summary>
public sealed class TraceFeedTests(TraceFeedFixture fixture)
    : IClassFixture<TraceFeedFixture>, IAsyncLifetime
{
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async ValueTask InitializeAsync()
    {
        fixture.TraceBuffer.Reset();
        _context = await fixture.Browser.NewContextAsync(new() { BaseURL = fixture.BaseUrl });
        _page = await _context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.CloseAsync();
    }

    private static AgentTraceEvent TraceEvent(
        string agentId, string toolName, string status = "success") => new()
    {
        TraceId = Guid.NewGuid().ToString("N")[..8],
        AgentId = agentId,
        ToolName = toolName,
        Status = status,
        Timestamp = DateTimeOffset.UtcNow.ToString("O"),
        TokensUsed = 100,
        LatencyMs = 42,
    };

    private static AgentTraceEvent UnidentifiedEvent(string toolName) => new()
    {
        TraceId = Guid.NewGuid().ToString("N")[..8],
        AgentId = string.Empty,
        ToolName = toolName,
        Status = "error",
        Timestamp = DateTimeOffset.UtcNow.ToString("O"),
    };

    [Fact]
    public async Task TraceFeedPage_WithoutSession_RedirectsToLogin()
    {
        await _page.GotoAsync("/dashboard/trace");

        _page.Url.Should().Contain("/dashboard/login");
    }

    [Fact]
    public async Task TraceFeedPage_HistoryReplay_ShowsBufferedEvents()
    {
        // Pre-seed events from two different agents before the page loads.
        // DashboardHubConnection replays buffer history into the component on subscribe.
        fixture.TraceBuffer.Seed(TraceEvent("agent-alpha", "GetInventory"));
        fixture.TraceBuffer.Seed(TraceEvent("agent-beta", "CreateOrder"));

        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/trace");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Assertions.Expect(_page.GetByText("agent-alpha")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("GetInventory")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("agent-beta")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("CreateOrder")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task TraceFeedPage_LiveEvent_AppearsWithoutRefresh()
    {
        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/trace");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for Blazor hydration so the component has registered its subscription handler.
        await _page.WaitForFunctionAsync(@"() =>
            Array.from(document.querySelectorAll('*')).some(el =>
                Array.from(el.attributes).some(a => a.name.startsWith('_bl_'))
            )");

        // Push a live event — no HTTP request, no page refresh.
        fixture.SubscriptionManager.NotifyAll(TraceEvent("live-agent", "SearchProducts"));

        // Blazor sends the render batch over the existing WebSocket. Poll until DOM updates.
        await Assertions.Expect(_page.GetByText("live-agent")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("SearchProducts")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task TraceFeedPage_FilterByAgent_ShowsOnlyMatchingEvents()
    {
        fixture.TraceBuffer.Seed(TraceEvent("agent-alpha", "GetInventory"));
        fixture.TraceBuffer.Seed(TraceEvent("agent-beta", "CreateOrder"));

        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/trace");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for Blazor hydration before typing into the filter input.
        await _page.WaitForFunctionAsync(@"() =>
            Array.from(document.querySelectorAll('*')).some(el =>
                Array.from(el.attributes).some(a => a.name.startsWith('_bl_'))
            )");

        // Both agents visible before filtering.
        await Assertions.Expect(_page.GetByText("agent-alpha")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("agent-beta")).ToBeVisibleAsync();

        // MudTextField with Immediate="true" binds on oninput — FillAsync is enough.
        await _page.GetByLabel("Filter by Agent ID").FillAsync("agent-alpha");

        // Blazor processes the oninput event over SignalR. Poll until the table updates.
        await Assertions.Expect(_page.GetByText("agent-beta")).ToBeHiddenAsync();
        await Assertions.Expect(_page.GetByText("agent-alpha")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task TraceFeedPage_ClearFilter_ShowsAllAgentEvents()
    {
        fixture.TraceBuffer.Seed(TraceEvent("agent-alpha", "GetInventory"));
        fixture.TraceBuffer.Seed(TraceEvent("agent-beta", "CreateOrder"));

        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/trace");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await _page.WaitForFunctionAsync(@"() =>
            Array.from(document.querySelectorAll('*')).some(el =>
                Array.from(el.attributes).some(a => a.name.startsWith('_bl_'))
            )");

        // Apply a filter so only alpha is shown.
        var filterInput = _page.GetByLabel("Filter by Agent ID");
        await filterInput.FillAsync("agent-alpha");
        await Assertions.Expect(_page.GetByText("agent-beta")).ToBeHiddenAsync();

        // Clear the filter — empty string maps to null in ActiveFilter, showing all agents.
        await filterInput.FillAsync(string.Empty);
        await Assertions.Expect(_page.GetByText("agent-beta")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("agent-alpha")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task TraceFeedPage_UnidentifiedEvent_AppearsInUnidentifiedTable()
    {
        // Events with an empty AgentId are routed to the Unidentified Traffic table.
        fixture.TraceBuffer.Seed(UnidentifiedEvent("SuspiciousTool"));

        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/trace");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Assertions.Expect(_page.GetByText("Unidentified Traffic")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("SuspiciousTool")).ToBeVisibleAsync();
    }
}
