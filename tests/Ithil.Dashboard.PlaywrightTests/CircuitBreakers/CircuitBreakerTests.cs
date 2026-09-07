using AwesomeAssertions;
using Ithil.Core.Models;
using Ithil.Dashboard.PlaywrightTests.Fixtures;
using Microsoft.Playwright;

namespace Ithil.Dashboard.PlaywrightTests.CircuitBreakers;

/// <summary>
/// Playwright tests for the Circuit Breakers dashboard page.
/// Covers redirect enforcement, each circuit state indicator, and live updates
/// pushed while the page is open (no browser refresh required).
/// </summary>
public sealed class CircuitBreakerTests(CircuitBreakerFixture fixture)
    : IClassFixture<CircuitBreakerFixture>, IAsyncLifetime
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

    // Builds a minimal circuit-state trace event.
    private static AgentTraceEvent CircuitEvent(string agentId, string toolName, string state) =>
        new()
        {
            TraceId = Guid.NewGuid().ToString("N")[..8],
            AgentId = agentId,
            ToolName = toolName,
            Status = "circuit",
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            CircuitState = state,
        };

    [Fact]
    public async Task CircuitsPage_WithoutSession_RedirectsToLogin()
    {
        await _page.GotoAsync("/dashboard/circuits");

        _page.Url.Should().Contain("/dashboard/login");
    }

    [Fact]
    public async Task CircuitsPage_WithNoEvents_ShowsEmptyMessage()
    {
        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/circuits");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Assertions.Expect(
            _page.GetByText("No circuit state changes received yet.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CircuitsPage_ClosedCircuit_ShowsClosedIndicator()
    {
        fixture.TraceBuffer.Seed(CircuitEvent("agent-1", "GetInventory", "closed"));

        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/circuits");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Assertions.Expect(_page.GetByText("agent-1")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("GetInventory")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("Closed")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CircuitsPage_OpenCircuit_ShowsOpenIndicator()
    {
        fixture.TraceBuffer.Seed(CircuitEvent("agent-2", "CreateOrder", "open"));

        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/circuits");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Assertions.Expect(_page.GetByText("agent-2")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("CreateOrder")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("Open")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CircuitsPage_HalfOpenCircuit_ShowsHalfOpenIndicator()
    {
        fixture.TraceBuffer.Seed(CircuitEvent("agent-3", "GetStock", "half-open"));

        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/circuits");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Assertions.Expect(_page.GetByText("agent-3")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("GetStock")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("Half-Open")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CircuitsPage_LiveEvent_UpdatesStateWithoutRefresh()
    {
        // Start with a closed circuit in the buffer.
        fixture.TraceBuffer.Seed(CircuitEvent("agent-4", "SearchProducts", "closed"));

        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/circuits");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Confirm the initial closed state is showing.
        await Assertions.Expect(_page.GetByText("Closed")).ToBeVisibleAsync();

        // Wait for Blazor hydration so the component has registered its subscription handler.
        await _page.WaitForFunctionAsync(@"() =>
            Array.from(document.querySelectorAll('*')).some(el =>
                Array.from(el.attributes).some(a => a.name.startsWith('_bl_'))
            )");

        // Push a live open event directly into the subscription manager — no HTTP request.
        // The component's OnTraceEvent handler will receive it via InvokeAsync and call StateHasChanged.
        fixture.SubscriptionManager.NotifyAll(
            CircuitEvent("agent-4", "SearchProducts", "open"));

        // Blazor sends the re-render over the existing WebSocket. Poll until the DOM updates.
        await Assertions.Expect(_page.GetByText("Open")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("Closed")).ToBeHiddenAsync();
    }
}
