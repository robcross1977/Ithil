using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Dashboard;
using Ithil.Dashboard.Auth;
using Ithil.Gateway;
using Ithil.Gateway.Hubs;
using Ithil.Gateway.Management;
using Ithil.Gateway.Mcp;
using Ithil.Gateway.Transforms;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIthilServices(builder.Configuration);
builder.Services.AddIthilManagement();
builder.Services.AddIthilDashboard();
builder
    .Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(context =>
    {
        context.AddRequestTransform(async transformContext =>
        {
            var pipeline =
                transformContext.HttpContext.RequestServices.GetRequiredService<RequestTransformPipeline>();
            await pipeline.TransformAsync(transformContext.HttpContext);
        });

        context.AddResponseTransform(async transformContext =>
        {
            if (transformContext.ProxyResponse?.Content is null)
                return;

            var pipeline =
                transformContext.HttpContext.RequestServices.GetRequiredService<ResponseTransformPipeline>();

            var agentId =
                transformContext.HttpContext.Items["Ithil.AgentId"] as string ?? string.Empty;
            var toolName =
                transformContext.HttpContext.Items["Ithil.ToolName"] as string ?? string.Empty;
            var traceId = transformContext
                .HttpContext.Request.Headers["X-Ithil-TraceId"]
                .ToString();
            var body = await transformContext.ProxyResponse.Content.ReadAsStreamAsync();

            var stopwatch =
                transformContext.HttpContext.Items["Ithil.Stopwatch"]
                as System.Diagnostics.Stopwatch;
            var latencyMs = stopwatch?.ElapsedMilliseconds;

            await pipeline.TransformAsync(agentId, traceId, toolName, body, latencyMs);
        });
    });
// Redis is always a required dependency: IConnectionMultiplexer, BudgetEngine, and
// SemanticCacheService are unconditionally Redis-backed regardless of UseInMemory.
// UseInMemory only swaps the agent/key *store* to in-memory — Redis still has to be
// reachable for budget enforcement and semantic caching to function.
builder.Services.AddHealthChecks()
    .AddCheck<Ithil.Gateway.Health.EmbeddingModelHealthCheck>(
        "embedding-model", tags: ["ready"])
    .AddCheck<Ithil.Gateway.Health.RedisHealthCheck>(
        "redis", tags: ["ready"]);
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
        options.ConfigureSessionOptions = McpSessionConfiguration.ConfigureSessionAsync);

var app = builder.Build();

// Resolve eagerly so the tiktoken download happens at startup, not on the first live request.
app.Services.GetRequiredService<ITokenCounter>();
// Resolve eagerly so the ONNX model loads at startup. The readiness probe reads
// IEmbeddingService.IsReady, which is only true once the constructor completes —
// eager resolution ensures the probe reflects real startup state.
app.Services.GetRequiredService<IEmbeddingService>();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseStaticFiles();
app.UseIthilDashboard();
// Live trace feed is operator-only — require the admin-scoped dashboard session cookie,
// not the agent JWT that gets issued to every connected agent.
app.MapHub<TraceHub>("/hubs/trace").RequireAuthorization(DashboardAuthPolicy.PolicyName);

// Seed a dev agent so identity resolution succeeds during local testing.
if (app.Environment.IsDevelopment())
{
    var configRepo = app.Services.GetRequiredService<IAgentConfigRepository>();
    await configRepo.UpsertAsync(
        new AgentConfig
        {
            AgentId = "dev-agent-01",
            Label = "Dev Test Agent",
            DailyTokenBudget = 100_000,
            IsActive = true,
        }
    );

    // Guards the /dev/* token endpoints: even inside IsDevelopment(), refuse any caller that
    // isn't on loopback. Stops accidental admin-token issuance if DOTNET_ENVIRONMENT leaks to
    // a shared/staging host.
    static bool IsLoopback(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress is { } ip &&
        (System.Net.IPAddress.IsLoopback(ip) ||
         ip.Equals(ctx.Connection.LocalIpAddress));

    // Temporary endpoint - generates a dev JWT for manual testing.
    app.MapGet(
        "/dev/token",
        (HttpContext ctx, IConfiguration config) =>
        {
            if (!IsLoopback(ctx))
                return Results.NotFound();
            var jwt = config.GetSection("Ithil:Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SigningKey"]!));
            var handler = new JsonWebTokenHandler();
            var token = handler.CreateToken(
                new SecurityTokenDescriptor
                {
                    Claims = new Dictionary<string, object> { { "agent_id", "dev-agent-01" } },
                    Expires = DateTime.UtcNow.AddHours(8),
                    Issuer = jwt["Issuer"],
                    Audience = jwt["Audience"],
                    SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
                }
            );
            return Results.Ok(new { token });
        }
    );

    // Generates a dashboard admin JWT for testing the /dashboard/login page.
    app.MapGet(
        "/dev/admin-token",
        (HttpContext ctx, IConfiguration config) =>
        {
            if (!IsLoopback(ctx))
                return Results.NotFound();
            var jwt = config.GetSection("Ithil:Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SigningKey"]!));
            var handler = new JsonWebTokenHandler();
            var token = handler.CreateToken(
                new SecurityTokenDescriptor
                {
                    Claims = new Dictionary<string, object> { { "scope", "admin" } },
                    Expires = DateTime.UtcNow.AddHours(8),
                    Issuer = jwt["Issuer"],
                    Audience = jwt["Audience"],
                    SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
                }
            );
            return Results.Ok(new { token });
        }
    );
}

// Liveness: is the process alive and not deadlocked? No dependency checks —
// a Redis outage must not cause Kubernetes to restart the pod.
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
});

// Readiness: runs every check tagged "ready". 503 diverts traffic but leaves
// the pod running so it can recover when its dependencies come back.
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});
app.MapManagementEndpoints();
app.MapMcp("/mcp").RequireAuthorization(ManagementAuthPolicy.AgentPolicyName);
app.MapReverseProxy().RequireAuthorization(ManagementAuthPolicy.AgentPolicyName);

app.Run();
