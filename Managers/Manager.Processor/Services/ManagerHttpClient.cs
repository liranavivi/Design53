using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Shared.Correlation;

namespace Manager.Processor.Services;

/// <summary>
/// HTTP client for communication with other entity managers with resilience patterns
/// </summary>
public class ManagerHttpClient : IManagerHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ManagerHttpClient> _logger;
    private readonly string _stepManagerBaseUrl;
    private readonly string _schemaManagerBaseUrl;
    private readonly IAsyncPolicy<HttpResponseMessage> _resilientPolicy;

    public ManagerHttpClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ManagerHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _stepManagerBaseUrl = configuration["ManagerUrls:StepManager"]
            ?? throw new InvalidOperationException("StepManager URL not configured");
        _schemaManagerBaseUrl = configuration["ManagerUrls:Schema"] ?? "http://localhost:5000";

        // Configure resilience policy with retry and circuit breaker
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarningWithCorrelation("Retry {RetryCount} for {Operation} after {Delay}ms. Reason: {Reason}",
                        retryCount, context.OperationKey, timespan.TotalMilliseconds, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });

        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, duration) =>
                {
                    _logger.LogErrorWithCorrelation("Circuit breaker opened for {Duration}s. Reason: {Reason}",
                        duration.TotalSeconds, exception.Exception?.Message ?? exception.Result?.StatusCode.ToString());
                },
                onReset: () =>
                {
                    _logger.LogInformationWithCorrelation("Circuit breaker reset - service is healthy again");
                });

        _resilientPolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }

    public async Task<bool> CheckProcessorReferencesInSteps(Guid processorId)
    {
        var url = $"{_stepManagerBaseUrl}/api/step/processor/{processorId}/exists";
        return await ExecuteProcessorCheck(url, "CheckProcessorReferencesInSteps", processorId);
    }

    public async Task<bool> CheckSchemaExists(Guid schemaId)
    {
        var url = $"{_schemaManagerBaseUrl}/api/schema/{schemaId}/exists";
        return await ExecuteSchemaCheck(url, "CheckSchemaExists", schemaId);
    }

    private async Task<bool> ExecuteProcessorCheck(string url, string operationName, Guid processorId)
    {
        try
        {
            _logger.LogDebugWithCorrelation("Starting {Operation} for ProcessorId: {ProcessorId}, URL: {Url}", 
                operationName, processorId, url);

            var context = new Context(operationName);
            
            var response = await _resilientPolicy.ExecuteAsync(async (ctx) =>
            {
                var httpResponse = await _httpClient.GetAsync(url);
                return httpResponse;
            }, context);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var hasReferences = bool.Parse(content);
                
                _logger.LogDebugWithCorrelation("Completed {Operation} for ProcessorId: {ProcessorId}. HasReferences: {HasReferences}", 
                    operationName, processorId, hasReferences);
                
                return hasReferences;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogErrorWithCorrelation("HTTP error in {Operation} for ProcessorId: {ProcessorId}. Status: {StatusCode}, Content: {Content}",
                    operationName, processorId, response.StatusCode, errorContent);
                
                throw new HttpRequestException($"HTTP {response.StatusCode}: {errorContent}");
            }
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Circuit breaker is open for {Operation}. ProcessorId: {ProcessorId}",
                operationName, processorId);
            throw new InvalidOperationException($"Processor reference validation service is currently unavailable (circuit breaker open). Operation rejected for safety.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogErrorWithCorrelation(ex, "HTTP request failed for {Operation}. ProcessorId: {ProcessorId}",
                operationName, processorId);
            throw new InvalidOperationException($"Processor reference validation failed due to communication error. Operation rejected for safety.", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogErrorWithCorrelation(ex, "Timeout in {Operation} for ProcessorId: {ProcessorId}",
                operationName, processorId);
            throw new InvalidOperationException($"Processor reference validation timed out. Operation rejected for safety.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Unexpected error in {Operation} for ProcessorId: {ProcessorId}",
                operationName, processorId);
            throw new InvalidOperationException($"Processor reference validation failed unexpectedly. Operation rejected for safety.", ex);
        }
    }

    private async Task<bool> ExecuteSchemaCheck(string url, string operationName, Guid schemaId)
    {
        try
        {
            _logger.LogDebugWithCorrelation("Starting {Operation} for SchemaId: {SchemaId}, URL: {Url}",
                operationName, schemaId, url);

            var context = new Context(operationName);

            var response = await _resilientPolicy.ExecuteAsync(async (ctx) =>
            {
                var httpResponse = await _httpClient.GetAsync(url);
                return httpResponse;
            }, context);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var exists = bool.Parse(content);

                _logger.LogDebugWithCorrelation("Completed {Operation} for SchemaId: {SchemaId}. Exists: {Exists}",
                    operationName, schemaId, exists);

                return exists;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogErrorWithCorrelation("HTTP error in {Operation} for SchemaId: {SchemaId}. Status: {StatusCode}, Content: {Content}",
                    operationName, schemaId, response.StatusCode, errorContent);

                throw new HttpRequestException($"HTTP {response.StatusCode}: {errorContent}");
            }
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Circuit breaker is open for {Operation}. SchemaId: {SchemaId}",
                operationName, schemaId);
            throw new InvalidOperationException($"Schema validation service is currently unavailable (circuit breaker open). Operation rejected for safety.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogErrorWithCorrelation(ex, "HTTP request failed for {Operation}. SchemaId: {SchemaId}",
                operationName, schemaId);
            throw new InvalidOperationException($"Schema validation failed due to communication error. Operation rejected for safety.", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogErrorWithCorrelation(ex, "Timeout in {Operation} for SchemaId: {SchemaId}",
                operationName, schemaId);
            throw new InvalidOperationException($"Schema validation timed out. Operation rejected for safety.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Unexpected error in {Operation} for SchemaId: {SchemaId}",
                operationName, schemaId);
            throw new InvalidOperationException($"Schema validation failed unexpectedly. Operation rejected for safety.", ex);
        }
    }
}
