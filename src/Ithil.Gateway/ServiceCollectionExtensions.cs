using Ithil.Core.Interfaces;
using Ithil.Gateway.Transforms;
using Ithil.Gateway.Stubs;

namespace Ithil.Gateway;

/// <summary>
/// Registers all Ithil gateway services into the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the request and response transform pipelines and all governance service interfaces.
    /// Concrete implementations will be registered as each feature sprint is completed.
    /// </summary>
    public static IServiceCollection AddIthilServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<RequestTransformPipeline>();
        services.AddScoped<ResponseTransformPipeline>();

        // Placeholder registrations - replace with real implementations in later sprints
        services.AddScoped<IAgentIdentityService, NotImplementedAgentIdentityService>();
        services.AddScoped<IBudgetEngine, NotImplementedBudgetEngine>();
        services.AddScoped<IToolAllowlistService, NotImplementedToolAllowlistService>();
        services.AddScoped<ITraceIdFactory, DefaultTraceIdFactory>();
        services.AddScoped<ITraceNotifier, NotImplementedTraceNotifier>();
        services.AddScoped<IPrivacyFilter, NotImplementedPrivacyFilter>();

        return services;
    }
}
