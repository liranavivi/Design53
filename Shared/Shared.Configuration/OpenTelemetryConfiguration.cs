using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Shared.Correlation;

namespace Shared.Configuration;

/// <summary>
/// Configuration extension methods for OpenTelemetry observability setup.
/// </summary>
public static class OpenTelemetryConfiguration
{
    /// <summary>
    /// Adds OpenTelemetry observability services to the service collection.
    /// Configures tracing, metrics, and logging with OTLP exporters and correlation ID enrichment.
    /// For managers, uses ManagerConfiguration for unique meter naming pattern.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="serviceName">The service name for OpenTelemetry. If not provided, reads from configuration.</param>
    /// <param name="serviceVersion">The service version for OpenTelemetry. If not provided, reads from configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddOpenTelemetryObservability(this IServiceCollection services, IConfiguration configuration, string? serviceName = null, string? serviceVersion = null)
    {
        // Get service name and version from parameters or configuration, with fallbacks
        serviceName ??= configuration["OpenTelemetry:ServiceName"] ?? "Manager.?????";
        serviceVersion ??= configuration["OpenTelemetry:ServiceVersion"] ?? "1.0.0";

        // Configure resource
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
            .AddTelemetrySdk()
            .AddEnvironmentVariableDetector();

        // Add OpenTelemetry
        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = httpContext =>
                        {
                            // Filter out health check requests
                            return !httpContext.Request.Path.StartsWithSegments("/health");
                        };
                    })
                    .AddHttpClientInstrumentation()
                    .AddSource($"{serviceName}.*")
                    .AddSource("MassTransit")
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317");
                        // Fix for 30-minute export failure - set explicit timeout values
                        options.TimeoutMilliseconds = 30000; // 30 seconds per export
                        options.Headers = "x-source=dotnet-traces";
                    });
            })
            .WithMetrics(builder =>
            {
                var metricsBuilder = builder
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter($"{serviceVersion}_{serviceName}.*")
                    .AddMeter($"{serviceVersion}_{serviceName}.File");


                metricsBuilder.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317");
                    // Fix for 30-minute metrics export failure - set explicit timeout values
                    options.TimeoutMilliseconds = 30000; // 30 seconds per export (default is 10s)
                    // Add headers to help with debugging
                    options.Headers = "x-source=dotnet-metrics";
                });
            });

        // Configure OpenTelemetry logging separately to integrate with .NET logging system
        services.AddLogging(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(resourceBuilder);

                options.AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri(configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317");
                    // Fix for 30-minute export failure - set explicit timeout values
                    otlpOptions.TimeoutMilliseconds = 30000; // 30 seconds per export
                    otlpOptions.Headers = "x-source=dotnet-logs";
                });

                // Add console exporter based on configuration setting (not just development)
                // This allows processors to send logs to both collector and console window
                var useConsoleExporter = configuration.GetValue<bool>("OpenTelemetry:UseConsoleExporter", true);

                if (useConsoleExporter)
                {
                    options.AddConsoleExporter();
                }
            });
        });

        // Add correlation ID context service
        services.AddSingleton<ICorrelationIdContext, CorrelationIdContext>();

        return services;
    }
}
