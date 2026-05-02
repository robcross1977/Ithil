using FluentAssertions;
using Ithil.Dashboard.PlaywrightTests.Fixtures;
using Ithil.Management.Models;
using Microsoft.Playwright;

namespace Ithil.Dashboard.PlaywrightTests.Agents;

/// <summary>
/// Playwright tests for the Agents dashboard page.
/// Covers create, list, and delete via the UI. Edit and allowlist management are
/// not yet implemented in the UI and are therefore not tested here.
/// </summary>
public sealed class AgentManagementTests(AgentManagementFixture fixture)
    : IClassFixture<AgentManagementFixture>, IAsyncLifetime
{
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async ValueTask InitializeAsync()
    {
        // Each test starts with an empty agent store and a fresh browser session.
        fixture.AgentService.Reset();
        _context = await fixture.Browser.NewContextAsync(new() { BaseURL = fixture.BaseUrl });
        _page = await _context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.CloseAsync();
    }

    [Fact]
    public async Task AgentsPage_WithoutSession_RedirectsToLogin()
    {
        await _page.GotoAsync("/dashboard/agents");

        _page.Url.Should().Contain("/dashboard/login");
    }

    [Fact]
    public async Task AgentsPage_WithSession_ShowsExistingAgents()
    {
        // Pre-seed two agents into the in-memory store before the page loads.
        await fixture.AgentService.CreateAsync(new CreateAgentRequest
        {
            Label = "Alpha Agent",
            DailyTokenBudget = 5_000,
        });
        await fixture.AgentService.CreateAsync(new CreateAgentRequest
        {
            Label = "Beta Agent",
            DailyTokenBudget = 10_000,
        });

        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/agents");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        (await _page.GetByText("Alpha Agent").IsVisibleAsync()).Should().BeTrue();
        (await _page.GetByText("Beta Agent").IsVisibleAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task CreateAgent_WithValidName_AppearsInListWithApiKeyAlert()
    {
        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/agents");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for Blazor to fully hydrate: the Create Agent button gets a _bl_ attribute
        // after the circuit applies its first render batch. Without this wait, FillAsync
        // races with hydration and the server resets the input to the empty initial state.
        await _page.WaitForFunctionAsync(@"() =>
            Array.from(document.querySelectorAll('button')).some(b =>
                Array.from(b.attributes).some(a => a.name.startsWith('_bl_'))
            )");

        // MudTextField uses @bind-Value which fires on the change event.
        // FillAsync sets the value, then pressing Tab moves focus away and fires change,
        // which Blazor routes over SignalR to update NewLabel on the server.
        var labelInput = _page.Locator("input[type='text']").First;
        await labelInput.FillAsync("My New Agent");
        await labelInput.PressAsync("Tab");

        // Blazor routes the change event via SignalR — wait for the server to reflect
        // the new value before clicking (the button is disabled while Label is empty).
        var createButton = _page.GetByRole(AriaRole.Button, new() { Name = "Create Agent" });
        await Assertions.Expect(createButton).ToBeEnabledAsync();
        await createButton.ClickAsync();

        // Blazor sends the render batch back over WebSocket (no new HTTP requests),
        // so WaitForLoadStateAsync(NetworkIdle) fires too early. Poll until the DOM updates.
        await Assertions.Expect(_page.GetByText("Agent created.")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("My New Agent")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CreateAgent_WithBlankLabel_CreateButtonIsDisabled()
    {
        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/agents");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Label is blank by default — the button must be disabled immediately.
        var button = _page.GetByRole(AriaRole.Button, new() { Name = "Create Agent" });
        (await button.IsDisabledAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task RevokeAgent_AfterConfirmation_RemovedFromList()
    {
        await fixture.AgentService.CreateAsync(new CreateAgentRequest
        {
            Label = "Doomed Agent",
            DailyTokenBudget = 1_000,
        });

        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/agents");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        // Wait for Blazor hydration before interacting with server-wired buttons.
        await _page.WaitForFunctionAsync(@"() =>
            Array.from(document.querySelectorAll('button')).some(b =>
                Array.from(b.attributes).some(a => a.name.startsWith('_bl_'))
            )");

        // Click the Revoke button in the table row.
        await _page.GetByRole(AriaRole.Button, new() { Name = "Revoke" }).ClickAsync();

        // MudMessageBox renders in a portal overlay with class mud-dialog.
        // Wait for the dialog to be visible before clicking the confirm button.
        var confirmButton = _page.Locator(".mud-dialog")
            .GetByRole(AriaRole.Button, new() { Name = "Revoke" });
        await Assertions.Expect(confirmButton).ToBeVisibleAsync();
        await confirmButton.ClickAsync();

        // Blazor sends the re-render over the existing WebSocket, not a new HTTP request,
        // so WaitForLoadStateAsync(NetworkIdle) fires immediately. Poll until DOM updates.
        await Assertions.Expect(_page.GetByText("Doomed Agent")).ToBeHiddenAsync();
    }
}
