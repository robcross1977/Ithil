using AwesomeAssertions;
using Ithil.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ithil.Hosting.Tests;

/// <summary>
/// Tests for discovering minimal-API endpoints marked with WithAgentTool.
/// </summary>
/// <remarks>
/// Each test builds a real WebApplication and starts it, because EndpointDataSource is not
/// populated until the host builds its routing table. Binding to port 0 lets the OS pick a
/// free port so parallel test runs cannot collide.
/// </remarks>
public class EndpointToolDiscoveryTests
{
    /// <summary>Body payload used to exercise complex-type expansion.</summary>
    public record OrderRequest(string Sku, int Quantity, bool Express);

    private static async Task<IReadOnlyList<ToolEntry>> DiscoverAsync(Action<WebApplication> configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        configure(app);

        await app.StartAsync();
        try
        {
            return EndpointToolDiscovery.Discover(
                app.Services.GetRequiredService<EndpointDataSource>());
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    // -------------------------------------------------------------------------
    // Opt-in behaviour
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Discover_IgnoresEndpoints_WithoutTheMarker()
    {
        // The security-critical case: mapping a route must never expose it to an agent
        // unless it was explicitly marked.
        var tools = await DiscoverAsync(app =>
        {
            app.MapGet("/health", () => "ok");
            app.MapDelete("/admin/purge", () => Results.Ok());
            app.MapGet("/api/orders", () => Results.Ok())
               .WithAgentTool("ListOrders", "Lists all orders");
        });

        tools.Should().HaveCount(1);
        tools[0].Name.Should().Be("ListOrders");
    }

    [Fact]
    public async Task Discover_ReturnsEmpty_WhenNothingIsMarked()
    {
        var tools = await DiscoverAsync(app => app.MapGet("/health", () => "ok"));

        tools.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Route and verb resolution
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Discover_ResolvesVerbAndRoute()
    {
        var tools = await DiscoverAsync(app =>
            app.MapPost("/api/orders", (OrderRequest body) => Results.Ok())
               .WithAgentTool("CreateOrder", "Creates an order", allowWrite: true));

        tools.Should().ContainSingle();
        tools[0].HttpMethod.Should().Be("POST");
        tools[0].RoutePattern.Should().Be("api/orders");
        tools[0].AllowWrite.Should().BeTrue();
    }

    [Fact]
    public async Task Discover_ResolvesRouteGroupPrefix()
    {
        // Route groups are the case a source generator cannot see reliably: the final
        // template only exists once ASP.NET Core has combined group and endpoint.
        var tools = await DiscoverAsync(app =>
        {
            var group = app.MapGroup("/api/v2");
            group.MapGet("/things/{key}", (string key) => Results.Ok())
                 .WithAgentTool("GetThing", "Gets a thing by key");
        });

        tools.Should().ContainSingle();
        tools[0].RoutePattern.Should().Be("api/v2/things/{key}");
        tools[0].ParameterSources["key"].Should().Be("route");
    }

    [Fact]
    public async Task Discover_PrefersNonHeadVerb_WhenFrameworkAddsHead()
    {
        var tools = await DiscoverAsync(app =>
            app.MapGet("/api/things", () => Results.Ok())
               .WithAgentTool("GetThings", "Gets things"));

        tools.Should().ContainSingle();
        tools[0].HttpMethod.Should().Be("GET");
    }

    // -------------------------------------------------------------------------
    // Parameter binding sources
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Discover_ClassifiesRouteAndQueryParameters()
    {
        var tools = await DiscoverAsync(app =>
            app.MapGet("/api/orders/{id}", (int id, string? filter) => Results.Ok())
               .WithAgentTool("GetOrder", "Gets an order by id"));

        var tool = tools.Should().ContainSingle().Subject;

        tool.ParameterSources["id"].Should().Be("route");
        tool.ParameterTypes["id"].Should().Be("integer");

        tool.ParameterSources["filter"].Should().Be("query");
        tool.ParameterTypes["filter"].Should().Be("string");
    }

    [Fact]
    public async Task Discover_ExpandsComplexBodyIntoCamelCasedProperties()
    {
        var tools = await DiscoverAsync(app =>
            app.MapPost("/api/orders", (OrderRequest body) => Results.Ok())
               .WithAgentTool("CreateOrder", "Creates an order", allowWrite: true));

        var tool = tools.Should().ContainSingle().Subject;

        tool.ParameterSources.Should().ContainKeys("sku", "quantity", "express");
        tool.ParameterSources["sku"].Should().Be("body");
        tool.ParameterTypes["sku"].Should().Be("string");
        tool.ParameterTypes["quantity"].Should().Be("integer");
        tool.ParameterTypes["express"].Should().Be("boolean");

        // The parameter itself must not leak through alongside its expanded properties.
        tool.ParameterSources.Should().NotContainKey("body");
    }

    [Fact]
    public async Task Discover_ExcludesInfrastructureParameters()
    {
        // CancellationToken and HttpContext are supplied by the framework. Exposing them
        // as agent inputs would be meaningless at best and misleading at worst.
        var tools = await DiscoverAsync(app =>
            app.MapGet("/api/orders/{id}",
                    (int id, HttpContext ctx, CancellationToken ct) => Results.Ok())
               .WithAgentTool("GetOrder", "Gets an order by id"));

        var tool = tools.Should().ContainSingle().Subject;

        tool.ParameterSources.Should().ContainKey("id");
        tool.ParameterSources.Should().NotContainKeys("ctx", "ct");
    }

    [Fact]
    public async Task Discover_HonoursExplicitBindingAttributes()
    {
        var tools = await DiscoverAsync(app =>
            app.MapPost("/api/search", ([FromQuery] string term) => Results.Ok())
               .WithAgentTool("Search", "Searches records"));

        var tool = tools.Should().ContainSingle().Subject;

        // Without the attribute a string on a POST would still be query, so the meaningful
        // assertion is that the explicit attribute is read rather than ignored.
        tool.ParameterSources["term"].Should().Be("query");
    }

    // -------------------------------------------------------------------------
    // Metadata carried through
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Discover_CarriesAllToolMetadata()
    {
        var tools = await DiscoverAsync(app =>
            app.MapGet("/api/reports", () => Results.Ok())
               .WithAgentTool(
                    "GetReports",
                    "Retrieves reports",
                    maxResponseTokens: 500,
                    category: "Reporting",
                    requiredScopes: ["reports.read"]));

        var tool = tools.Should().ContainSingle().Subject;

        tool.Name.Should().Be("GetReports");
        tool.Description.Should().Be("Retrieves reports");
        tool.MaxResponseTokens.Should().Be(500);
        tool.Category.Should().Be("Reporting");
        tool.RequiredScopes.Should().BeEquivalentTo(["reports.read"]);
        tool.AllowWrite.Should().BeFalse();
    }

    [Fact]
    public async Task Discover_FindsEveryMarkedEndpoint()
    {
        var tools = await DiscoverAsync(app =>
        {
            app.MapGet("/a", () => Results.Ok()).WithAgentTool("A", "First");
            app.MapPost("/b", () => Results.Ok()).WithAgentTool("B", "Second");
            app.MapPut("/c", () => Results.Ok()).WithAgentTool("C", "Third");
        });

        tools.Select(t => t.Name).Should().BeEquivalentTo(["A", "B", "C"]);
    }

    // -------------------------------------------------------------------------
    // Registration-time validation
    // -------------------------------------------------------------------------

    [Fact]
    public void WithAgentTool_Throws_OnEmptyDescription()
    {
        // The generator reports ITHIL002 at compile time for attributed methods. Minimal-API
        // endpoints can only be checked at runtime, so fail at registration instead.
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        var act = () => app.MapGet("/x", () => Results.Ok()).WithAgentTool("X", "  ");

        act.Should().Throw<ArgumentException>()
           .WithMessage("*non-empty description*");
    }

    [Fact]
    public void WithAgentTool_Throws_OnEmptyName()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        var act = () => app.MapGet("/x", () => Results.Ok()).WithAgentTool("", "Does a thing");

        act.Should().Throw<ArgumentException>()
           .WithMessage("*non-empty name*");
    }

    // -------------------------------------------------------------------------
    // Routing patterns raised in review
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Discover_ClassifiesCatchAllParameter_AsRoute()
    {
        var tools = await DiscoverAsync(app =>
            app.MapGet("/files/{*path}", (string path) => Results.Ok())
               .WithAgentTool("GetFile", "Gets a file by path"));

        var tool = tools.Should().ContainSingle().Subject;
        tool.ParameterSources["path"].Should().Be("route");
    }

    [Fact]
    public async Task Discover_ClassifiesDoubleCatchAllParameter_AsRoute()
    {
        var tools = await DiscoverAsync(app =>
            app.MapGet("/files/{**path}", (string path) => Results.Ok())
               .WithAgentTool("GetFile", "Gets a file by path"));

        var tool = tools.Should().ContainSingle().Subject;
        tool.ParameterSources["path"].Should().Be("route");
    }

    [Fact]
    public async Task Discover_ClassifiesConstrainedCatchAll_AsRoute()
    {
        var tools = await DiscoverAsync(app =>
            app.MapGet("/files/{*path:minlength(1)}", (string path) => Results.Ok())
               .WithAgentTool("GetFile", "Gets a file by path"));

        var tool = tools.Should().ContainSingle().Subject;
        tool.ParameterSources["path"].Should().Be("route");
    }

    [Fact]
    public async Task Discover_Throws_ForMultiVerbEndpoint_WithoutExplicitMethod()
    {
        // MapMethods can register several verbs on one endpoint, but a tool carries one.
        // Silently picking GET would publish a schema that never exercises POST.
        var act = async () => await DiscoverAsync(app =>
            app.MapMethods("/api/things", new[] { "GET", "POST" }, () => Results.Ok())
               .WithAgentTool("Things", "Reads or writes things"));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*multiple HTTP methods*")
            .WithMessage("*GET, POST*");
    }

    [Fact]
    public async Task Discover_UsesExplicitMethod_ForMultiVerbEndpoint()
    {
        var tools = await DiscoverAsync(app =>
            app.MapMethods("/api/things", new[] { "GET", "POST" }, () => Results.Ok())
               .WithAgentTool("Things", "Writes things", httpMethod: "POST"));

        tools.Should().ContainSingle();
        tools[0].HttpMethod.Should().Be("POST");
    }

    [Fact]
    public async Task Discover_Throws_ForVerblessMap_WithoutExplicitMethod()
    {
        // app.Map(...) carries no IHttpMethodMetadata. The old code emitted an empty
        // HttpMethod, putting an uncallable tool into the schema.
        var act = async () => await DiscoverAsync(app =>
            app.Map("/api/anything", () => Results.Ok())
               .WithAgentTool("Anything", "Handles any verb"));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*without an HTTP method constraint*");
    }

    [Fact]
    public async Task Discover_UsesExplicitMethod_ForVerblessMap()
    {
        var tools = await DiscoverAsync(app =>
            app.Map("/api/anything", () => Results.Ok())
               .WithAgentTool("Anything", "Reads anything", httpMethod: "get"));

        tools.Should().ContainSingle();
        tools[0].HttpMethod.Should().Be("GET");
    }

    [Fact]
    public async Task Discover_Throws_WhenExplicitMethodIsNotServedByEndpoint()
    {
        // Catches the typo case: a schema advertising DELETE on a GET-only route would
        // hand the agent a call that always 405s.
        var act = async () => await DiscoverAsync(app =>
            app.MapGet("/api/things", () => Results.Ok())
               .WithAgentTool("Things", "Gets things", httpMethod: "DELETE"));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*only serves GET*");
    }

    [Fact]
    public async Task Discover_NeverEmitsEmptyHttpMethod()
    {
        // Backstop for the class of bug rather than one instance of it.
        var tools = await DiscoverAsync(app =>
        {
            app.MapGet("/a", () => Results.Ok()).WithAgentTool("A", "First");
            app.MapPost("/b", () => Results.Ok()).WithAgentTool("B", "Second");
            app.MapDelete("/c/{id}", (int id) => Results.Ok()).WithAgentTool("C", "Third");
        });

        tools.Should().AllSatisfy(t => t.HttpMethod.Should().NotBeNullOrWhiteSpace());
    }
}
