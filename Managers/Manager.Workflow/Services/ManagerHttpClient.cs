using Polly;
using Polly.Extensions.Http;
using Shared.Correlation;

namespace Manager.Workflow.Services;

/// <summary>
/// HTTP client service for communicating with other managers for validation
/// </summary>
public class ManagerHttpClient : IManagerHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ManagerHttpClient> _logger;
    private readonly string _stepManagerBaseUrl;
    private readonly IAsyncPolicy<HttpResponseMessage> _resilientPolicy;

    public ManagerHttpClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ManagerHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Get Step Manager URL from configuration
        _stepManagerBaseUrl = configuration["Services:StepManager:BaseUrl"] ?? "http://localhost:60891";
        
        // Create resilient policy with retry and circuit breaker
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarningWithCorrelation("Retry {RetryCount} for HTTP request after {Delay}ms. Reason: {Reason}",
                        retryCount, timespan.TotalMilliseconds, outcome.Exception?.Message ?? outcome.Result?.ReasonPhrase);
                });

        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, duration) =>
                {
                    _logger.LogWarningWithCorrelation("Circuit breaker opened for {Duration}s. Reason: {Reason}",
                        duration.TotalSeconds, exception.Exception?.Message ?? exception.Result?.ReasonPhrase);
                },
                onReset: () =>
                {
                    _logger.LogInformationWithCorrelation("Circuit breaker reset - requests will be allowed again");
                });

        _resilientPolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }

    public async Task<bool> CheckStepExists(Guid stepId)
    {
        var url = $"{_stepManagerBaseUrl}/api/step/{stepId}/exists";
        return await ExecuteStepExistenceCheck(url, "StepExistenceCheck", stepId);
    }

    private async Task<bool> ExecuteStepExistenceCheck(string url, string operationName, Guid stepId)
    {
        _logger.LogDebugWithCorrelation("Starting {OperationName} for StepId: {StepId}, URL: {Url}", 
            operationName, stepId, url);

        try
        {
            var response = await _resilientPolicy.ExecuteAsync(async () =>
            {
                var httpResponse = await _httpClient.GetAsync(url);
                
                if (httpResponse.IsSuccessStatusCode)
                {
                    return httpResponse;
                }

                _logger.LogWarningWithCorrelation("{OperationName} returned non-success status. StepId: {StepId}, StatusCode: {StatusCode}, URL: {Url}",
                    operationName, stepId, httpResponse.StatusCode, url);
                
                // For non-success status codes, we'll treat as service unavailable
                throw new HttpRequestException($"Service returned {httpResponse.StatusCode}");
            });

            var content = await response.Content.ReadAsStringAsync();
            var exists = bool.Parse(content);

            _logger.LogDebugWithCorrelation("{OperationName} completed successfully. StepId: {StepId}, Exists: {Exists}",
                operationName, stepId, exists);

            return exists;
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException ex)
        {
            _logger.LogErrorWithCorrelation(ex, "{OperationName} failed - circuit breaker is open. StepId: {StepId}",
                operationName, stepId);

            // Fail-safe: If service unavailable, reject operation for data integrity
            throw new InvalidOperationException($"Step validation service unavailable (circuit breaker open). Operation rejected for data integrity.");
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "{OperationName} failed. StepId: {StepId}, URL: {Url}",
                operationName, stepId, url);
            
            // Fail-safe: If service unavailable, reject operation for data integrity
            throw new InvalidOperationException($"Step validation service unavailable. Operation rejected for data integrity: {ex.Message}");
        }
    }
}
