using System.Text;
using System.Threading.Channels;
using Ithil.Budget;
using Ithil.Cache;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Hubs;
using Ithil.Gateway.Identity;
using Ithil.Gateway.Stubs;
using Ithil.Gateway.Transforms;
using Ithil.Management.Audit;
using Ithil.Management.Audit.Sinks;
using Ithil.Management.Repositories;
using Ithil.Privacy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.ML.Tokenizers;
using Polly;
using StackExchange.Redis;

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

        var tokenValidationParameters = BuildTokenValidationParameters(configuration);
        services.AddSingleton(tokenValidationParameters);
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => options.TokenValidationParameters = tokenValidationParameters);
        services.AddAuthorization();
        services.AddScoped<IJwtIdentityResolver, JwtIdentityResolver>();
        services.AddScoped<IApiKeyIdentityResolver, ApiKeyIdentityResolver>();
        services.AddSingleton<IAgentConfigRepository, AgentConfigRepository>();
        services.AddScoped<IApiKeyRepository, NotImplementedApiKeyRepository>();
        services.AddScoped<IAgentIdentityService, AgentIdentityService>();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(
                configuration.GetConnectionString("Redis")
                    ?? throw new InvalidOperationException("ConnectionStrings:Redis is required")
            )
        );
        services.AddScoped(sp => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());     
        services.AddSingleton(
            new BudgetEngineOptions
            {
                DefaultDailyTokenLimit = configuration.GetValue(
                    "Ithil:Budget:DefaultDailyTokenLimit",
                    100_000
                ),
            }
        );
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

        services.AddSingleton(
            new SemanticCacheOptions
            {
                ModelPath =
                    configuration["Ithil:SemanticCache:ModelPath"]
                    ?? "models/all-MiniLM-L6-v2.onnx",
                VocabPath = configuration["Ithil:SemanticCache:VocabPath"] ?? "models/vocab.txt",
            }
        );
        services.AddSingleton<IEmbeddingService, EmbeddingService>();
        services.AddScoped<ISemanticCache, SemanticCacheService>();

        var circuitBreakerOptions = new Resilience.CircuitBreakerOptions();
        configuration.GetSection("Ithil:CircuitBreaker").Bind(circuitBreakerOptions);
        services.AddSingleton(circuitBreakerOptions);

        services
            .AddHttpClient("downstream")
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
        services.AddSingleton<ITraceNotifier, TraceNotifier>();
        services.AddSingleton<PrivacyFilterOptions>();
        services.AddScoped<IPrivacyFilter, PrivacyFilterService>();

        services.AddIthilAudit(configuration);

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
