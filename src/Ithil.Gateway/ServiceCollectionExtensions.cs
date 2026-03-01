using Ithil.Budget;
using Ithil.Core.Interfaces;
using Ithil.Gateway.Identity;
using Ithil.Gateway.Stubs;
using Ithil.Gateway.Transforms;
using Ithil.Management.Repositories;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;

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
        IConfiguration configuration)
    {
        services.AddScoped<RequestTransformPipeline>();
        services.AddScoped<ResponseTransformPipeline>();

        services.AddSingleton(BuildTokenValidationParameters(configuration));
        services.AddScoped<IJwtIdentityResolver, JwtIdentityResolver>();
        services.AddScoped<IApiKeyIdentityResolver, ApiKeyIdentityResolver>();
        services.AddSingleton<IAgentConfigRepository, AgentConfigRepository>();
        services.AddScoped<IApiKeyRepository, NotImplementedApiKeyRepository>();
        services.AddScoped<IAgentIdentityService, AgentIdentityService>();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(
                configuration.GetConnectionString("Redis")
                    ?? throw new InvalidOperationException("ConnectionStrings:Redis is required")));
        services.AddScoped<IDatabase>(sp =>
            sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());
        services.AddSingleton(new BudgetEngineOptions
        {
            DefaultDailyTokenLimit = configuration.GetValue<int>("Ithil:Budget:DefaultDailyTokenLimit", 100_000)
        });
        services.AddScoped<IBudgetEngine, BudgetEngine>();

        services.AddScoped<IToolAllowlistService, NotImplementedToolAllowlistService>();
        services.AddScoped<ITraceIdFactory, DefaultTraceIdFactory>();
        services.AddScoped<ITraceNotifier, NotImplementedTraceNotifier>();
        services.AddScoped<IPrivacyFilter, NotImplementedPrivacyFilter>();

        return services;
    }

    private static TokenValidationParameters BuildTokenValidationParameters(IConfiguration configuration)
    {
        var jwt = configuration.GetSection("Ithil:Jwt");
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwt["SigningKey"]
                ?? throw new InvalidOperationException("Ithil:Jwt:SigningKey is required")));

        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateLifetime = true
        };
    }
}
