using Polly;
using Polly.CircuitBreaker;
using Shared.Correlation;

namespace Manager.Delivery.Services;

/// <summary>
/// HTTP client for communicating with other entity managers with resilience patterns
/// </summary>
public class ManagerHttpClient : IManagerHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ManagerHttpClient> _logger;
    private readonly string _assignmentManagerBaseUrl;
    private readonly string _schemaManagerBaseUrl;

    // Retry policy with exponential backoff
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    
    // Circuit breaker policy
    private readonly IAsyncPolicy<HttpResponseMessage> _circuitBreakerPolicy;
    
    // Combined policy
    private readonly IAsyncPolicy<HttpResponseMessage> _resilientPolicy;

    public ManagerHttpClient(HttpClient httpClient, IConfiguration configuration, ILogger<ManagerHttpClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Get manager URLs from configuration
        _assignmentManagerBaseUrl = configuration["ManagerUrls:Assignment"] ?? "http://localhost:60887";
        _schemaManagerBaseUrl = configuration["ManagerUrls:Schema"] ?? "http://localhost:5000";

        // Configure retry policy with exponential backoff
        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarningWithCorrelation("Retry {RetryCount} for {Operation} after {Delay}ms. Reason: {Reason}",
                        retryCount, context.OperationKey, timespan.TotalMilliseconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });

        // Configure circuit breaker
        _circuitBreakerPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
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

        // Combine policies: retry first, then circuit breaker
        _resilientPolicy = Policy.WrapAsync(_retryPolicy, _circuitBreakerPolicy);
    }

    public async Task<bool> CheckEntityReferencesInAssignments(Guid entityId)
    {
        var url = $"{_assignmentManagerBaseUrl}/api/assignment/entity/{entityId}/exists";
        return await ExecuteEntityCheck(url, "AssignmentEntityCheck", entityId);
    }

    public async Task<bool> CheckSchemaExists(Guid schemaId)
    {
        var url = $"{_schemaManagerBaseUrl}/api/schema/{schemaId}/exists";
        return await ExecuteEntityCheck(url, "SchemaExistenceCheck", schemaId);
    }

    private async Task<bool> ExecuteEntityCheck(string url, string operationName, Guid entityId)
    {
        try
        {
            _logger.LogDebugWithCorrelation("Starting {Operation} for EntityId: {EntityId}, URL: {Url}", 
                operationName, entityId, url);

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
                
                _logger.LogDebugWithCorrelation("Completed {Operation} for EntityId: {EntityId}. HasReferences: {HasReferences}", 
                    operationName, entityId, hasReferences);
                
                return hasReferences;
            }
            else
            {
                _logger.LogErrorWithCorrelation("Failed {Operation} for EntityId: {EntityId}. StatusCode: {StatusCode}, URL: {Url}", 
                    operationName, entityId, response.StatusCode, url);
                
                // Fail-safe approach: if we can't validate, assume there are references
                throw new InvalidOperationException($"Entity validation service unavailable. StatusCode: {response.StatusCode}");
            }
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Circuit breaker is open for {Operation}. EntityId: {EntityId}", 
                operationName, entityId);
            
            // Fail-safe: if circuit is open, assume there are references
            throw new InvalidOperationException($"Entity validation service is currently unavailable due to circuit breaker. Operation: {operationName}");
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            _logger.LogErrorWithCorrelation(ex, "Unexpected error during {Operation} for EntityId: {EntityId}", 
                operationName, entityId);
            
            // Fail-safe: on unexpected errors, assume there are references
            throw new InvalidOperationException($"Entity validation failed due to unexpected error. Operation: {operationName}", ex);
        }
    }
}
