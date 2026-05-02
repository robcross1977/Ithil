using Ithil.Core;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Dashboard;
using Ithil.Dashboard.Auth;
using Ithil.Management.Models;
using Ithil.Management.Services;
using LanguageExt;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Playwright;
using NSubstitute;
using System.Text;

namespace Ithil.Dashboard.PlaywrightTests.Fixtures;

/// <summary>
/// Spins up a minimal dashboard host on a random port and launches a Chromium browser.
/// Shared across all tests in a class via IClassFixture — each test creates its own
/// browser context so cookies don't bleed between tests.
/// </summary>
public class DashboardFixture : IAsyncLifetime
{
    // Test signing key — never used in production.
    private const string SigningKey = "ithil-playwright-test-key-32bytes!";
    private const string Issuer = "ithil-test";
    private const string Audience = "ithil-gateway";

    private WebApplication? _app;
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;
    public string BaseUrl { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _app = BuildHost(CreateAgentService(), CreateBudgetQueryService(), CreateTraceBuffer(), CreateSubscriptionManager());
        await _app.StartAsync();

        // Resolve the actual port assigned by the OS (we bound to port 0).
        var addresses = _app.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>();
        BaseUrl = addresses!.Addresses.First();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        _playwright?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
    }

    /// <summary>
    /// Navigates to the login page and signs in with a valid admin token.
    /// Call this at the start of any test that requires an authenticated session.
    /// </summary>
    public async Task LoginAsync(IPage page)
    {
        await page.GotoAsync("/dashboard/login");
        await page.Locator("#token").FillAsync(AdminToken());
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
    }

    /// <summary>
    /// Returns the <see cref="IAgentManagementService"/> registered with the test host.
    /// Override in subclasses to substitute a different implementation.
    /// </summary>
    protected virtual IAgentManagementService CreateAgentService()
    {
        var mock = Substitute.For<IAgentManagementService>();
        mock.GetAllAsync()
            .Returns(Task.FromResult(
                Either<ManagementError, Seq<AgentResponse>>.Right(Seq<AgentResponse>.Empty)));
        return mock;
    }

    /// <summary>
    /// Returns the <see cref="IBudgetQueryService"/> registered with the test host.
    /// Override in subclasses to provide pre-configured budget data.
    /// </summary>
    protected virtual IBudgetQueryService CreateBudgetQueryService() =>
        Substitute.For<IBudgetQueryService>();

    /// <summary>
    /// Returns the <see cref="ITraceBuffer"/> registered with the test host.
    /// Override in subclasses to pre-seed trace history.
    /// </summary>
    protected virtual ITraceBuffer CreateTraceBuffer()
    {
        var mock = Substitute.For<ITraceBuffer>();
        mock.GetRecent(Arg.Any<int>()).Returns(Seq<AgentTraceEvent>.Empty);
        return mock;
    }

    /// <summary>
    /// Returns the <see cref="ITraceSubscriptionManager"/> registered with the test host.
    /// Override in subclasses to enable live event delivery to components.
    /// </summary>
    protected virtual ITraceSubscriptionManager CreateSubscriptionManager() =>
        Substitute.For<ITraceSubscriptionManager>();

    /// <summary>Generates a valid admin-scoped JWT for the test signing key.</summary>
    public string AdminToken(DateTimeOffset? expires = null) =>
        MakeToken(new Dictionary<string, object> { ["scope"] = "admin" }, expires);

    /// <summary>Generates a valid JWT without admin scope.</summary>
    public string NonAdminToken() =>
        MakeToken(new Dictionary<string, object> { ["role"] = "viewer" }, null);

    private static string MakeToken(Dictionary<string, object> claims, DateTimeOffset? expires)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Claims = claims,
            Issuer = Issuer,
            Audience = Audience,
            Expires = (expires ?? DateTimeOffset.UtcNow.AddHours(1)).UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });
    }

    private static WebApplication BuildHost(
        IAgentManagementService agentService,
        IBudgetQueryService budgetService,
        ITraceBuffer traceBuffer,
        ITraceSubscriptionManager subscriptionManager)
    {
        // ApplicationName must match the assembly whose static-web-asset manifest
        // MapStaticAssets() should load. CreateBuilder() defaults to Assembly.GetEntryAssembly()
        // which in an xUnit process is the test runner, not this project — so the manifest
        // for _framework/blazor.web.js would never be found.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(DashboardFixture).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
        });
        // Load static web assets (Blazor framework JS, MudBlazor CSS/JS) from
        // their NuGet package locations into WebRootFileProvider so UseStaticFiles serves them.
        builder.WebHost.UseStaticWebAssets();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        // Token validation for Login.cshtml.cs
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        builder.Services.AddSingleton(new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        });

        builder.Services.AddSingleton(agentService);
        builder.Services.AddSingleton(budgetService);
        builder.Services.AddSingleton(traceBuffer);
        builder.Services.AddSingleton(subscriptionManager);
        builder.Services.Configure<TraceOptions>(o => o.BufferSize = 100);

        builder.Services.AddIthilDashboard();

        // The dashboard hardens cookies for production (HTTPS-only, Strict SameSite).
        // Override both for the HTTP test host so the session cookie is actually set.
        builder.Services.PostConfigure<CookieAuthenticationOptions>(
            DashboardAuthPolicy.CookieScheme,
            options =>
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.None;
                options.Cookie.SameSite = SameSiteMode.Lax;
            });

        var app = builder.Build();

        // UseStaticFiles serves from WebRootFileProvider populated by UseStaticWebAssets().
        // This intercepts _framework and _content requests before MapStaticAssets' endpoint
        // handler (which returns Content-Length:0 in this test host).
        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.UseIthilDashboard();

        return app;
    }

}
