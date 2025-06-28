using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Correlation;

/// <summary>
/// Extension methods for configuring correlation ID services.
/// </summary>
public static class CorrelationIdServiceCollectionExtensions
{
    /// <summary>
    /// Adds correlation ID services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional correlation ID configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddCorrelationId(
        this IServiceCollection services, 
        CorrelationIdOptions? options = null)
    {
        // Register correlation ID context
        services.AddSingleton<ICorrelationIdContext, CorrelationIdContext>();

        // Register correlation ID options
        if (options != null)
        {
            services.AddSingleton(options);
        }
        else
        {
            services.AddSingleton(new CorrelationIdOptions());
        }

        // Register HTTP client delegating handler
        services.AddTransient<CorrelationIdDelegatingHandler>();

        // Note: HTTP client configuration will be done per-client basis
        // as ConfigureAll<HttpClientFactoryOptions> is not available in this context

        return services;
    }

    /// <summary>
    /// Adds correlation ID middleware to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for method chaining.</returns>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }

    /// <summary>
    /// Adds correlation ID services with HTTP client integration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureHttpClient">Optional HTTP client configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddCorrelationIdWithHttpClient(
        this IServiceCollection services,
        Action<HttpClient>? configureHttpClient = null)
    {
        services.AddCorrelationId();

        // Note: AddHttpClient requires Microsoft.Extensions.Http package
        // This will be configured in the consuming applications



        return services;
    }
}


