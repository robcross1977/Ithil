using Ithil.Dashboard.Auth;
using Ithil.Dashboard.Hubs;
using Ithil.Dashboard.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Ithil.Dashboard;

/// <summary>
/// Extension methods for mounting the Ithil operator dashboard on a WebApplication.
/// </summary>
public static class DashboardMiddlewareExtensions
{
    /// <summary>
    /// Registers all services required by the dashboard: Blazor Server, MudBlazor,
    /// cookie authentication, the dashboard authorization policy, and the four
    /// dashboard service classes.
    /// Call this on builder.Services before builder.Build().
    /// </summary>
    public static IServiceCollection AddIthilDashboard(this IServiceCollection services)
    {
        services.AddRazorPages();
        services.AddRazorComponents()
            .AddInteractiveServerComponents();
        services.AddMudServices();
        services.AddAuthentication()
            .AddCookie(DashboardAuthPolicy.CookieScheme, options =>
            {
                options.LoginPath = "/dashboard/login";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                // Cookie hardening: HTTPS-only, no cross-site sends, not accessible to scripts.
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
                options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
            });
        services.AddAuthorizationBuilder()
            .AddDashboardPolicy();
        services.AddScoped<TraceFeedService>();
        services.AddScoped<BudgetDashboardService>();
        services.AddScoped<CircuitDashboardService>();
        services.AddScoped<AgentDashboardService>();
        services.AddScoped<DashboardHubConnection>();

        return services;
    }

    /// <summary>
    /// Maps the dashboard login Razor page, Blazor components, and static assets
    /// into the request pipeline. Nothing is mounted if this method is not called.
    /// </summary>
    public static WebApplication UseIthilDashboard(this WebApplication app)
    {
        app.MapRazorPages();
        // Gate every Blazor dashboard route at the endpoint level. The Blazor router uses
        // RouteView rather than AuthorizeRouteView, so page-level [Authorize] attributes
        // would not otherwise be honored. Unauthenticated requests get redirected to the
        // cookie scheme's login path (/dashboard/login, served by MapRazorPages above).
        app.MapRazorComponents<DashboardApp>()
            .AddInteractiveServerRenderMode()
            .RequireAuthorization(DashboardAuthPolicy.PolicyName);

        return app;
    }
}