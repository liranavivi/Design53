using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Shared.Correlation;

namespace Shared.HealthChecks;

public class OpenTelemetryHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenTelemetryHealthCheck> _logger;

    public OpenTelemetryHealthCheck(
        HttpClient httpClient, 
        IConfiguration configuration, 
        ILogger<OpenTelemetryHealthCheck> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use the OpenTelemetry Collector health check endpoint instead of the gRPC endpoint
            var healthEndpoint = _configuration["OpenTelemetry:HealthEndpoint"] ?? "http://localhost:8081";

            using var response = await _httpClient.GetAsync(healthEndpoint, cancellationToken);

            // Check if the health endpoint returns a successful response
            response.EnsureSuccessStatusCode();

            _logger.LogDebugWithCorrelation("OpenTelemetry health check completed successfully. Status: {StatusCode}", response.StatusCode);

            return HealthCheckResult.Healthy("OpenTelemetry Collector is healthy");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "OpenTelemetry endpoint health check failed");
            return HealthCheckResult.Unhealthy("OpenTelemetry endpoint is not reachable", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "OpenTelemetry endpoint health check timed out");
            return HealthCheckResult.Unhealthy("OpenTelemetry endpoint health check timed out", ex);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Unexpected error during OpenTelemetry health check");
            return HealthCheckResult.Unhealthy("Unexpected error during OpenTelemetry health check", ex);
        }
    }
}
