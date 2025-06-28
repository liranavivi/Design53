using Polly;
using Polly.CircuitBreaker;
using Shared.Correlation;

namespace Manager.Schema.Services;

/// <summary>
/// HTTP client for communicating with other entity managers with resilience patterns
/// </summary>
public class ManagerHttpClient : IManagerHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ManagerHttpClient> _logger;
    private readonly string _addressManagerBaseUrl;
    private readonly string _deliveryManagerBaseUrl;
    private readonly string _processorManagerBaseUrl;

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
        _addressManagerBaseUrl = configuration["ManagerUrls:Address"] ?? "http://localhost:60884";
        _deliveryManagerBaseUrl = configuration["ManagerUrls:Delivery"] ?? "http://localhost:60885";
        _processorManagerBaseUrl = configuration["ManagerUrls:Processor"] ?? "http://localhost:60886";

        // Configure retry policy with exponential backoff
        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarningWithCorrelation("Retry {RetryCount} for {Operation} after {Delay}ms. Reason: {Reason}",
                        retryCount, context.OperationKey, timespan.TotalMilliseconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });

        // Configure circuit breaker policy
        _circuitBreakerPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
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

        // Combine policies: Circuit breaker wraps retry
        _resilientPolicy = Policy.WrapAsync(_circuitBreakerPolicy, _retryPolicy);
    }

    public async Task<bool> CheckAddressSchemaReferences(Guid schemaId)
    {
        var url = $"{_addressManagerBaseUrl}/api/address/schema/{schemaId}/exists";
        return await ExecuteSchemaCheck(url, "AddressSchemaCheck", schemaId);
    }

    public async Task<bool> CheckDeliverySchemaReferences(Guid schemaId)
    {
        var url = $"{_deliveryManagerBaseUrl}/api/delivery/schema/{schemaId}/exists";
        return await ExecuteSchemaCheck(url, "DeliverySchemaCheck", schemaId);
    }

    public async Task<bool> CheckProcessorInputSchemaReferences(Guid schemaId)
    {
        var url = $"{_processorManagerBaseUrl}/api/processor/input-schema/{schemaId}/exists";
        return await ExecuteSchemaCheck(url, "ProcessorInputSchemaCheck", schemaId);
    }

    public async Task<bool> CheckProcessorOutputSchemaReferences(Guid schemaId)
    {
        var url = $"{_processorManagerBaseUrl}/api/processor/output-schema/{schemaId}/exists";
        return await ExecuteSchemaCheck(url, "ProcessorOutputSchemaCheck", schemaId);
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
                var hasReferences = bool.Parse(content);
                
                _logger.LogDebugWithCorrelation("Completed {Operation} for SchemaId: {SchemaId}. HasReferences: {HasReferences}", 
                    operationName, schemaId, hasReferences);
                
                return hasReferences;
            }
            else
            {
                _logger.LogErrorWithCorrelation("Failed {Operation} for SchemaId: {SchemaId}. StatusCode: {StatusCode}, URL: {Url}", 
                    operationName, schemaId, response.StatusCode, url);
                
                // Fail-safe approach: if we can't validate, assume there are references
                throw new InvalidOperationException($"Schema validation service unavailable. StatusCode: {response.StatusCode}");
            }
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Circuit breaker is open for {Operation}. SchemaId: {SchemaId}", 
                operationName, schemaId);
            
            // Fail-safe: if circuit is open, assume there are references
            throw new InvalidOperationException($"Schema validation service is currently unavailable due to circuit breaker. Operation: {operationName}");
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error during {Operation} for SchemaId: {SchemaId}, URL: {Url}", 
                operationName, schemaId, url);
            
            // Fail-safe: if validation fails, assume there are references
            throw new InvalidOperationException($"Schema validation failed. Operation: {operationName}, Error: {ex.Message}");
        }
    }
}
