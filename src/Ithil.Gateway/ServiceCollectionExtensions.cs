using Ithil.Budget;
using Ithil.Cache;
using Ithil.Core;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Hubs;
using Ithil.Gateway.Identity;
using Ithil.Gateway.Management;
using Ithil.Gateway.Options;
using Ithil.Gateway.Stubs;
using Ithil.Gateway.Transforms;
using Ithil.Management.Audit;
using Ithil.Management.Audit.Sinks;
using Ithil.Management.Repositories;
using Ithil.Management.Services;
using Ithil.Privacy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.ML.Tokenizers;
using Polly;
using StackExchange.Redis;
using System.Text;
using System.Threading.Channels;

namespace Ithil.Gateway;

/// <summary>
/// Registers all Ithil gateway services into the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the request and response transform pipelines and all governance service interfaces.
    /// </summary>
    public static IServiceCollection AddIthilServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddScoped<RequestTransformPipeline>();
        services.AddScoped<ResponseTransformPipeline>();
        services.AddScoped<ToolCallGovernancePipeline>();

        var tokenValidationParameters = BuildTokenValidationParameters(configuration);
        services.AddSingleton(tokenValidationParameters);
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => options.TokenValidationParameters = tokenValidationParameters);
        services.AddAuthorization();
        services.AddAuthorizationBuilder()
            .AddManagementPolicy()
            .AddAgentPolicy();
        services.AddScoped<IJwtIdentityResolver, JwtIdentityResolver>();
        services.AddScoped<IApiKeyIdentityResolver, ApiKeyIdentityResolver>();
        if (configuration.GetValue<bool>("Ithil:AgentStore:UseInMemory"))
            services.AddSingleton<IAgentConfigRepository, InMemoryAgentConfigRepository>();
        else
            services.AddSingleton<IAgentConfigRepository, RedisAgentConfigRepository>();
        if (configuration.GetValue<bool>("Ithil:AgentStore:UseInMemory"))
            services.AddSingleton<IApiKeyRepository, InMemoryApiKeyRepository>();
        else
            services.AddSingleton<IApiKeyRepository, RedisApiKeyRepository>();
        services.AddScoped<IAgentIdentityService, AgentIdentityService>();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(
                configuration.GetConnectionString("Redis")
                    ?? throw new InvalidOperationException("ConnectionStrings:Redis is required")
            )
        );
        services.AddScoped(sp => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());     
        var budgetEngineOptions = new BudgetEngineOptions
        {
            DefaultDailyTokenLimit = configuration.GetValue(
                "Ithil:Budget:DefaultDailyTokenLimit",
                100_000
            ),
            FailurePolicy = configuration.GetValue(
                "Ithil:Budget:FailurePolicy",
                Ithil.Core.Enums.RedisFailurePolicy.FailOpen
            ),
        };
        services.AddSingleton(budgetEngineOptions);
        services.AddSingleton<ITokenCounter>(_ =>
        {
            using var http = new HttpClient();
            using var vocabStream = http.GetStreamAsync(
                "https://openaipublic.blob.core.windows.net/encodings/cl100k_base.tiktoken"
            ).GetAwaiter().GetResult();
            var tokenizer = TiktokenTokenizer.CreateForModelAsync("gpt-4", vocabStream)
                .GetAwaiter().GetResult();
            return new Tokenization.TiktokenTokenCounter(tokenizer);
        });
        services.AddScoped<IBudgetEngine, BudgetEngine>();

        var cacheOptions = new SemanticCacheOptions
        {
            ModelPath =
                configuration["Ithil:SemanticCache:ModelPath"]
                ?? "models/all-MiniLM-L6-v2.onnx",
            VocabPath = configuration["Ithil:SemanticCache:VocabPath"] ?? "models/vocab.txt",
            FailurePolicy = configuration.GetValue(
                "Ithil:SemanticCache:FailurePolicy",
                Ithil.Core.Enums.RedisFailurePolicy.FailOpen
            ),
        };
        services.AddSingleton(cacheOptions);
        services.AddSingleton<IEmbeddingService, EmbeddingService>();
        services.AddScoped<ISemanticCache, SemanticCacheService>();

        var circuitBreakerOptions = new Resilience.CircuitBreakerOptions();
        configuration.GetSection("Ithil:CircuitBreaker").Bind(circuitBreakerOptions);
        services.AddSingleton(circuitBreakerOptions);

        services
            .AddHttpClient("downstream")
            .ConfigureHttpClient(client =>
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", GenerateServiceToken(configuration)))
            .AddResilienceHandler(
                "circuit-breaker",
                (builder, context) =>
                {
                    var notifier = context.ServiceProvider.GetRequiredService<ITraceNotifier>();
                    var cbOptions =
                        context.ServiceProvider.GetRequiredService<Resilience.CircuitBreakerOptions>();
                    builder.AddCircuitBreaker(
                        Resilience.CircuitBreakerPolicyFactory.CreateStrategyOptions(
                            cbOptions,
                            notifier,
                            "gateway",
                            "downstream"
                        )
                    );
                }
            );
        var toolRegistryOptions = new Mcp.ToolRegistryOptions();
        configuration.GetSection("Ithil:ToolRegistry").Bind(toolRegistryOptions);
        services.AddSingleton(toolRegistryOptions);
        services.AddSingleton<IToolRegistry, Mcp.ToolRegistryService>();

        services.AddScoped<IToolAllowlistService, ToolAllowlistService>();
        services.AddScoped<ITraceIdFactory, DefaultTraceIdFactory>();
        services.AddSignalR();
        var traceOptions = new TraceOptions();
        configuration.GetSection("Ithil:Trace").Bind(traceOptions);
        if (traceOptions.BufferSize <= 0)
            throw new InvalidOperationException($"Ithil:Trace:BufferSize must be greater than 0; configured value: {traceOptions.BufferSize}.");
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(traceOptions));
        services.AddSingleton<ITraceBuffer, Tracing.TraceRingBuffer>();
        services.AddSingleton<ITraceNotifier, TraceNotifier>();
        services.AddSingleton<ITraceSubscriptionManager, TraceSubscriptionManager>();
        services.AddSingleton<PrivacyFilterOptions>();
        services.AddScoped<IPrivacyFilter, PrivacyFilterService>();

        services.AddIthilAudit(configuration);

        var shutdownOptions = new ShutdownOptions();
        configuration.GetSection("Ithil:Shutdown").Bind(shutdownOptions);
        if (shutdownOptions.TimeoutSeconds <= 0)
            throw new InvalidOperationException(
                $"Ithil:Shutdown:TimeoutSeconds must be greater than 0; configured value: {shutdownOptions.TimeoutSeconds}.");
        services.AddSingleton(shutdownOptions);
        services.Configure<HostOptions>(hostOptions =>
            hostOptions.ShutdownTimeout = TimeSpan.FromSeconds(shutdownOptions.TimeoutSeconds));

        if (budgetEngineOptions.FailurePolicy == Ithil.Core.Enums.RedisFailurePolicy.FailOpen ||
            cacheOptions.FailurePolicy == Ithil.Core.Enums.RedisFailurePolicy.FailOpen)
            Console.WriteLine(
                "[Ithil] WARNING: Redis failure policy is FailOpen. Budget enforcement and " +
                "semantic caching will be bypassed if Redis becomes unavailable. " +
                "Set Ithil:Budget:FailurePolicy and Ithil:SemanticCache:FailurePolicy " +
                "= FailClosed to reject requests instead.");

        return services;
    }

    /// <summary>
    /// Registers audit logging services. StdoutAuditSink is on by default;
    /// set Ithil:Audit:DisableStdoutSink = true to opt out.
    /// </summary>
    public static IServiceCollection AddIthilAudit(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var auditOptions = new AuditOptions();
        configuration.GetSection("Ithil:Audit").Bind(auditOptions);
        services.AddSingleton(auditOptions);

        services.AddSingleton(Channel.CreateUnbounded<AuditRecord>());
        services.AddSingleton<IAuditLogger, AuditLogger>();
        services.AddHostedService<AuditBackgroundWorker>();

        if (!auditOptions.DisableStdoutSink)
            services.AddSingleton<IAuditSink, StdoutAuditSink>();

        return services;
    }

    /// <summary>
    /// Registers management services for agent lifecycle and budget operations.
    /// </summary>
    public static IServiceCollection AddIthilManagement(this IServiceCollection services)
    {
        services.AddScoped<IAgentManagementService, AgentManagementService>();
        services.AddScoped<IBudgetQueryService, BudgetQueryService>();
        return services;
    }

    private static string GenerateServiceToken(IConfiguration configuration)
    {
        var jwt = configuration.GetSection("Ithil:Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            jwt["SigningKey"] ?? throw new InvalidOperationException("Ithil:Jwt:SigningKey is required")));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object> { { "agent_id", "gateway-service" } },
            Issuer = jwt["Issuer"] ?? throw new InvalidOperationException("Ithil:Jwt:Issuer is required"),
            Audience = jwt["Audience"] ?? throw new InvalidOperationException("Ithil:Jwt:Audience is required"),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });
    }

    private static TokenValidationParameters BuildTokenValidationParameters(
        IConfiguration configuration
    )
    {
        var jwt = configuration.GetSection("Ithil:Jwt");
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                jwt["SigningKey"]
                    ?? throw new InvalidOperationException("Ithil:Jwt:SigningKey is required")
            )
        );

        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateLifetime = true,
        };
    }
}
