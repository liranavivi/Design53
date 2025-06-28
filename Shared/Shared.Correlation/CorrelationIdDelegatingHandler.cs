using Microsoft.Extensions.Logging;

namespace Shared.Correlation;

/// <summary>
/// HTTP client delegating handler that automatically adds correlation ID headers
/// to outgoing HTTP requests for cross-service correlation.
/// </summary>
public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private readonly ICorrelationIdContext _correlationIdContext;
    private readonly ILogger<CorrelationIdDelegatingHandler> _logger;
    private readonly string _headerName;

    public CorrelationIdDelegatingHandler(
        ICorrelationIdContext correlationIdContext,
        ILogger<CorrelationIdDelegatingHandler> logger,
        string headerName = "X-Correlation-ID")
    {
        _correlationIdContext = correlationIdContext;
        _logger = logger;
        _headerName = headerName;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        var correlationId = _correlationIdContext.Current;
        
        if (correlationId != Guid.Empty)
        {
            // Add correlation ID header if not already present
            if (!request.Headers.Contains(_headerName))
            {
                request.Headers.Add(_headerName, correlationId.ToString());
                _logger.LogDebugWithCorrelation("Added correlation ID to outgoing request: {CorrelationId} to {RequestUri}", 
                    correlationId, request.RequestUri);
            }
        }
        else
        {
            _logger.LogWarningWithCorrelation("No correlation ID available for outgoing request to {RequestUri}", 
                request.RequestUri);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
