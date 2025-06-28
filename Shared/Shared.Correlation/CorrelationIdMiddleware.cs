using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Shared.Correlation;

/// <summary>
/// Middleware that extracts or generates correlation IDs from HTTP requests
/// and ensures they are available throughout the request pipeline.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    private readonly ICorrelationIdContext _correlationIdContext;
    private readonly CorrelationIdOptions _options;

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger,
        ICorrelationIdContext correlationIdContext,
        CorrelationIdOptions? options = null)
    {
        _next = next;
        _logger = logger;
        _correlationIdContext = correlationIdContext;
        _options = options ?? new CorrelationIdOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ExtractOrGenerateCorrelationId(context);
        
        // Set correlation ID in context
        _correlationIdContext.Set(correlationId);

        // Add correlation ID to current activity
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetTag("correlation.id", correlationId.ToString());
            activity.SetBaggage("correlation.id", correlationId.ToString());
        }

        // Add correlation ID to response headers
        if (_options.IncludeInResponse)
        {
            context.Response.Headers.TryAdd(_options.HeaderName, correlationId.ToString());
        }

        _logger.LogDebugWithCorrelation("Correlation ID set for request: {CorrelationId}", correlationId);

        try
        {
            await _next(context);
        }
        finally
        {
            // Clear correlation ID context after request
            _correlationIdContext.Clear();
        }
    }

    private Guid ExtractOrGenerateCorrelationId(HttpContext context)
    {
        // Try to extract from request headers
        if (context.Request.Headers.TryGetValue(_options.HeaderName, out var headerValues))
        {
            var headerValue = headerValues.ToString();
            if (!string.IsNullOrEmpty(headerValue) && Guid.TryParse(headerValue, out var correlationId))
            {
                _logger.LogDebugWithCorrelation("Correlation ID extracted from header: {CorrelationId}", correlationId);
                return correlationId;
            }
        }

        // Try to extract from Activity baggage (for internal service calls)
        var activity = Activity.Current;
        if (activity?.GetBaggageItem("correlation.id") is string baggageValue &&
            Guid.TryParse(baggageValue, out var baggageCorrelationId))
        {
            _logger.LogDebugWithCorrelation("Correlation ID extracted from Activity baggage: {CorrelationId}", baggageCorrelationId);
            return baggageCorrelationId;
        }

        // Generate new correlation ID
        var newCorrelationId = Guid.NewGuid();
        _logger.LogDebugWithCorrelation("New correlation ID generated: {CorrelationId}", newCorrelationId);
        return newCorrelationId;
    }
}

/// <summary>
/// Configuration options for correlation ID middleware.
/// </summary>
public class CorrelationIdOptions
{
    /// <summary>
    /// The header name to use for correlation ID. Default is "X-Correlation-ID".
    /// </summary>
    public string HeaderName { get; set; } = "X-Correlation-ID";

    /// <summary>
    /// Whether to include the correlation ID in response headers. Default is true.
    /// </summary>
    public bool IncludeInResponse { get; set; } = true;
}
