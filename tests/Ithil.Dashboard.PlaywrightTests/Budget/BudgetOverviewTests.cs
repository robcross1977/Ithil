using FluentAssertions;
using Ithil.Dashboard.PlaywrightTests.Fixtures;
using Ithil.Management.Models;
using Microsoft.Playwright;

namespace Ithil.Dashboard.PlaywrightTests.Budget;

/// <summary>
/// Playwright tests for the Budget Overview dashboard page.
/// Covers redirect enforcement, gauge percentage text, warning vs success colour,
/// and multiple agents each rendering their own card.
/// </summary>
public sealed class BudgetOverviewTests(BudgetFixture fixture)
    : IClassFixture<BudgetFixture>, IAsyncLifetime
{
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async ValueTask InitializeAsync()
    {
        // Each test starts with a clean agent store and a fresh browser session.
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
    public async Task BudgetPage_WithoutSession_RedirectsToLogin()
    {
        await _page.GotoAsync("/dashboard/budget");

        _page.Url.Should().Contain("/dashboard/login");
    }

    [Fact]
    public async Task BudgetPage_AgentWithNoUsage_ShowsZeroPercent()
    {
        var agent = await fixture.AgentService.CreateAsync(new CreateAgentRequest
        {
            Label = "Idle Agent",
            DailyTokenBudget = 10_000,
        });
        var agentId = agent.Match(Right: r => r.AgentId, Left: _ => string.Empty);
        fixture.SetBudget(agentId, tokensUsed: 0, dailyBudget: 10_000);

        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/budget");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The gauge caption shows "0 / 10,000 tokens (0.0%)"
        await Assertions.Expect(_page.GetByText("Idle Agent")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("(0.0%)")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task BudgetPage_AgentAtFiftyPercent_ShowsSuccessGauge()
    {
        var agent = await fixture.AgentService.CreateAsync(new CreateAgentRequest
        {
            Label = "Half Used Agent",
            DailyTokenBudget = 10_000,
        });
        var agentId = agent.Match(Right: r => r.AgentId, Left: _ => string.Empty);
        fixture.SetBudget(agentId, tokensUsed: 5_000, dailyBudget: 10_000);

        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/budget");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Assertions.Expect(_page.GetByText("Half Used Agent")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("(50.0%)")).ToBeVisibleAsync();

        // Below the 80% warning threshold — progress bar uses success colour.
        var card = _page.Locator(".mud-card").Filter(new() { HasText = "Half Used Agent" });
        await Assertions.Expect(card.Locator(".mud-progress-linear-color-success")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task BudgetPage_AgentAboveEightyPercent_ShowsWarningGauge()
    {
        var agent = await fixture.AgentService.CreateAsync(new CreateAgentRequest
        {
            Label = "Heavy Agent",
            DailyTokenBudget = 10_000,
        });
        var agentId = agent.Match(Right: r => r.AgentId, Left: _ => string.Empty);
        fixture.SetBudget(agentId, tokensUsed: 8_500, dailyBudget: 10_000);

        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/budget");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Assertions.Expect(_page.GetByText("Heavy Agent")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("(85.0%)")).ToBeVisibleAsync();

        // At 85% — above the 80% threshold — progress bar uses warning colour.
        var card = _page.Locator(".mud-card").Filter(new() { HasText = "Heavy Agent" });
        await Assertions.Expect(card.Locator(".mud-progress-linear-color-warning")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task BudgetPage_MultipleAgents_EachCardShowsCorrectValues()
    {
        var alphaResult = await fixture.AgentService.CreateAsync(new CreateAgentRequest
        {
            Label = "Alpha Agent",
            DailyTokenBudget = 20_000,
        });
        var betaResult = await fixture.AgentService.CreateAsync(new CreateAgentRequest
        {
            Label = "Beta Agent",
            DailyTokenBudget = 5_000,
        });

        var alphaId = alphaResult.Match(Right: r => r.AgentId, Left: _ => string.Empty);
        var betaId  = betaResult.Match(Right: r => r.AgentId, Left: _ => string.Empty);

        fixture.SetBudget(alphaId, tokensUsed: 2_000, dailyBudget: 20_000);  // 10%
        fixture.SetBudget(betaId,  tokensUsed: 4_500, dailyBudget: 5_000);   // 90%

        await fixture.LoginAsync(_page);
        await _page.GotoAsync("/dashboard/budget");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Alpha card: 10% — success
        var alphaCard = _page.Locator(".mud-card").Filter(new() { HasText = "Alpha Agent" });
        await Assertions.Expect(alphaCard.GetByText("(10.0%)")).ToBeVisibleAsync();
        await Assertions.Expect(alphaCard.Locator(".mud-progress-linear-color-success")).ToBeVisibleAsync();

        // Beta card: 90% — warning
        var betaCard = _page.Locator(".mud-card").Filter(new() { HasText = "Beta Agent" });
        await Assertions.Expect(betaCard.GetByText("(90.0%)")).ToBeVisibleAsync();
        await Assertions.Expect(betaCard.Locator(".mud-progress-linear-color-warning")).ToBeVisibleAsync();
    }
}
