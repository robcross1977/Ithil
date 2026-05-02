using Ithil.Budget;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway;
using Ithil.Integration.Tests.Stubs;
using Ithil.Gateway.Transforms;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;
using Testcontainers.Redis;

namespace Ithil.Integration.Tests.Fixtures;

/// <summary>
/// Spins up a Redis container and a minimal gateway test server once per test collection.
/// Tests access the server via <see cref="Client"/> and pre-configure agents through
/// <see cref="AgentConfigRepo"/> and <see cref="ApiKeyRepo"/>.
/// </summary>
public sealed class GatewayFixture : IAsyncLifetime
{
    // Test JWT signing parameters — never used in production.
    public const string SigningKey = "ithil-integration-test-signing-key-32b!";
    public const string Issuer = "ithil-test";
    public const string Audience = "ithil-gateway";

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private WebApplication? _app;

    public HttpClient Client { get; private set; } = null!;
    public IAgentConfigRepository AgentConfigRepo { get; private set; } = null!;
    public IApiKeyRepository ApiKeyRepo { get; private set; } = null!;
    public IConnectionMultiplexer Redis { get; private set; } = null!;

    /// <summary>
    /// Captures audit records written by the gateway pipeline during tests.
    /// The background worker drains the channel asynchronously, so tests should
    /// await <see cref="WaitForAuditRecordAsync"/> before asserting.
    /// </summary>
    public CapturingAuditSink AuditSink { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _redis.StartAsync();
        _app = BuildApp(_redis.GetConnectionString(), AuditSink);
        await _app.StartAsync();
        Client = _app.GetTestClient();
        AgentConfigRepo = _app.Services.GetRequiredService<IAgentConfigRepository>();
        ApiKeyRepo = _app.Services.GetRequiredService<IApiKeyRepository>();
        Redis = _app.Services.GetRequiredService<IConnectionMultiplexer>();
    }

    /// <summary>
    /// Polls until a record matching the predicate appears in the audit sink, or times out.
    /// Required because the audit background worker drains the channel asynchronously.
    /// </summary>
    public async Task<AuditRecord> WaitForAuditRecordAsync(
        Func<AuditRecord, bool> predicate,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(3));
        while (DateTime.UtcNow < deadline)
        {
            var record = AuditSink.Records.FirstOrDefault(predicate);
            if (record is not null) return record;
            await Task.Delay(25);
        }
        throw new TimeoutException("Audit record matching predicate did not appear within the timeout.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null) await _app.DisposeAsync();
        await _redis.DisposeAsync();
    }

    /// <summary>
    /// Creates a signed JWT carrying an <c>agent_id</c> claim.
    /// Pass a different <paramref name="signingKey"/> to simulate a wrong-key scenario.
    /// </summary>
    public string CreateAgentJwt(
        string agentId,
        DateTimeOffset? expires = null,
        string? signingKey = null)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(signingKey ?? SigningKey));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object> { ["agent_id"] = agentId },
            Issuer = Issuer,
            Audience = Audience,
            Expires = (expires ?? DateTimeOffset.UtcNow.AddHours(1)).UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });
    }

    /// <summary>
    /// Seeds a token usage value directly into Redis, bypassing the budget engine,
    /// so tests can start with a pre-exhausted budget without making real requests.
    /// </summary>
    public async Task SeedBudgetUsageAsync(string agentId, int tokensUsed)
    {
        var db = Redis.GetDatabase();
        var key = BudgetKeyFactory.ForToday(agentId);
        await db.StringSetAsync(key, tokensUsed);
        await db.KeyExpireAsync(key, TimeSpan.FromDays(2));
    }

    private static WebApplication BuildApp(string redisConnectionString, CapturingAuditSink auditSink)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ithil:Jwt:SigningKey"] = SigningKey,
            ["Ithil:Jwt:Issuer"] = Issuer,
            ["Ithil:Jwt:Audience"] = Audience,
            ["Ithil:AgentStore:UseInMemory"] = "true",
            ["Ithil:Trace:BufferSize"] = "100",
            ["Ithil:Shutdown:TimeoutSeconds"] = "30",
            ["Ithil:Audit:DisableStdoutSink"] = "true",
            ["Ithil:ToolRegistry:DownstreamBaseUrl"] = "http://localhost:1",
            ["ConnectionStrings:Redis"] = redisConnectionString,
        });

        builder.Services.AddIthilServices(builder.Configuration);

        // Replace the real tiktoken counter (downloads from internet at startup) with a stub.
        ReplaceService<ITokenCounter>(builder.Services, new StubTokenCounter());
        // Replace the real ONNX embedding service (requires model files) with a stub.
        ReplaceService<IEmbeddingService>(builder.Services, new StubEmbeddingService());
        // Register capturing sink so tests can assert on audit records written by the pipeline.
        builder.Services.AddSingleton<IAuditSink>(auditSink);

        var app = builder.Build();

        // A test endpoint that exercises the full request governance pipeline.
        // The path segment after /api/ becomes the tool name the allowlist check sees,
        // matching how the real pipeline extracts the tool name from context.Request.Path.
        app.Use(async (ctx, next) =>
        {
            var pipeline = ctx.RequestServices.GetRequiredService<RequestTransformPipeline>();
            await pipeline.TransformAsync(ctx);
            // The pipeline sets StatusCode (401/429/403) when blocking, but does not write
            // a response body — so HasStarted stays false even when blocked. Check StatusCode
            // directly to decide whether to call the downstream endpoint handler.
            if (ctx.Response.StatusCode < 400)
                await next(ctx);
        });
        app.MapGet("/api/{tool}", (string tool) => Results.Ok(new { allowed = true, tool }));

        return app;
    }

    private static void ReplaceService<T>(IServiceCollection services, T instance)
        where T : class
    {
        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(T));
        if (existing is not null) services.Remove(existing);
        services.AddSingleton<T>(instance);
    }
}

/// <summary>
/// xUnit collection definition — one Redis container and gateway server for all integration tests.
/// </summary>
[CollectionDefinition(Name)]
public sealed class GatewayCollection : ICollectionFixture<GatewayFixture>
{
    public const string Name = "Gateway";
}
