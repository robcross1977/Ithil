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
builder.Services.AddHealthChecks();
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
        options.ConfigureSessionOptions = McpSessionConfiguration.ConfigureSessionAsync);

var app = builder.Build();

// Resolve eagerly so the tiktoken download happens at startup, not on the first live request.
app.Services.GetRequiredService<ITokenCounter>();

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

    // Temporary endpoint - generates a dev JWT for manual testing.
    app.MapGet(
        "/dev/token",
        (IConfiguration config) =>
        {
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
        (IConfiguration config) =>
        {
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

app.MapHealthChecks("/health");
app.MapManagementEndpoints();
app.MapMcp("/mcp").RequireAuthorization(ManagementAuthPolicy.AgentPolicyName);
app.MapReverseProxy().RequireAuthorization(ManagementAuthPolicy.AgentPolicyName);

app.Run();
