using FluentAssertions;
using Ithil.Dashboard.PlaywrightTests.Fixtures;
using Microsoft.Playwright;

namespace Ithil.Dashboard.PlaywrightTests.Auth;

/// <summary>
/// Playwright tests for operator authentication and dashboard access.
/// Each test gets a fresh browser context so session cookies never bleed between cases.
/// </summary>
public sealed class LoginTests(DashboardFixture fixture)
    : IClassFixture<DashboardFixture>, IAsyncLifetime
{
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async ValueTask InitializeAsync()
    {
        _context = await fixture.Browser.NewContextAsync(new() { BaseURL = fixture.BaseUrl });
        _page = await _context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.CloseAsync();
    }

    [Fact]
    public async Task NavigatingToDashboard_WithoutSession_RedirectsToLogin()
    {
        await _page.GotoAsync("/dashboard");

        _page.Url.Should().Contain("/dashboard/login");
    }

    [Fact]
    public async Task Login_WithValidAdminToken_RedirectsToDashboard()
    {
        await _page.GotoAsync("/dashboard/login");
        await _page.Locator("#token").FillAsync(fixture.AdminToken());
        await _page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        _page.Url.Should().Contain("/dashboard");
        _page.Url.Should().NotContain("/login");
    }

    [Fact]
    public async Task Login_WithInvalidToken_ShowsErrorAndStaysOnLoginPage()
    {
        await _page.GotoAsync("/dashboard/login");
        await _page.Locator("#token").FillAsync("not.a.valid.jwt");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        _page.Url.Should().Contain("/dashboard/login");
        (await _page.Locator("p").Filter(new() { HasText = "Invalid token." }).IsVisibleAsync())
            .Should().BeTrue();
    }

    [Fact]
    public async Task Login_WithNonAdminToken_ShowsErrorAndStaysOnLoginPage()
    {
        await _page.GotoAsync("/dashboard/login");
        await _page.Locator("#token").FillAsync(fixture.NonAdminToken());
        await _page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        _page.Url.Should().Contain("/dashboard/login");
        (await _page.Locator("p")
            .Filter(new() { HasText = "Token does not have admin scope." })
            .IsVisibleAsync())
            .Should().BeTrue();
    }

    [Fact]
    public async Task SessionCleared_SubsequentNavigation_RedirectsToLogin()
    {
        // Establish a valid session first.
        await _page.GotoAsync("/dashboard/login");
        await _page.Locator("#token").FillAsync(fixture.AdminToken());
        await _page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        _page.Url.Should().Contain("/dashboard");

        // Simulate session expiry by wiping all cookies.
        await _context.ClearCookiesAsync();

        await _page.GotoAsync("/dashboard");

        _page.Url.Should().Contain("/dashboard/login");
    }

    [Fact]
    public async Task Logout_ClearsSession_RedirectsToLogin()
    {
        await _page.GotoAsync("/dashboard/login");
        await _page.Locator("#token").FillAsync(fixture.AdminToken());
        await _page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        // Start the URL waiter BEFORE clicking so the listener is registered before the
        // redirect from /dashboard/logout → /dashboard/login can fire and be missed.
        var navigated = _page.WaitForURLAsync("**/dashboard/login");
        await _page.GetByRole(AriaRole.Link, new() { Name = "Sign out" }).ClickAsync();
        await navigated;

        _page.Url.Should().Contain("/dashboard/login");
    }
}
