using System;
using System.Threading.Tasks;
using MassTransit;
using Shared.Correlation;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Shared.MassTransit;

/// <summary>
/// MassTransit consume filter that extracts correlation ID from incoming message headers
/// and sets it in the correlation context for the duration of message processing.
/// </summary>
public class CorrelationIdConsumeFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    private readonly ICorrelationIdContext _correlationIdContext;
    private readonly ILogger<CorrelationIdConsumeFilter<T>> _logger;

    public CorrelationIdConsumeFilter(
        ICorrelationIdContext correlationIdContext,
        ILogger<CorrelationIdConsumeFilter<T>> logger)
    {
        _correlationIdContext = correlationIdContext;
        _logger = logger;
    }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var correlationId = ExtractCorrelationId(context);
        
        if (correlationId != Guid.Empty)
        {
            // Set correlation ID in context
            _correlationIdContext.Set(correlationId);

            // Also set in current activity if available
            var activity = Activity.Current;
            if (activity != null)
            {
                activity.SetTag("correlation.id", correlationId.ToString());
                activity.SetBaggage("correlation.id", correlationId.ToString());
            }

            _logger.LogDebugWithCorrelation("Extracted correlation ID from incoming message: {CorrelationId} for message type {MessageType}", 
                correlationId, typeof(T).Name);
        }
        else
        {
            _logger.LogWarningWithCorrelation("No correlation ID found in incoming message of type {MessageType}", typeof(T).Name);
        }

        try
        {
            await next.Send(context);
        }
        finally
        {
            // Clear correlation ID context after message processing
            _correlationIdContext.Clear();
        }
    }

    private Guid ExtractCorrelationId(ConsumeContext<T> context)
    {
        // Try to extract from message headers
        if (context.Headers.TryGetHeader("X-Correlation-ID", out var headerValue) &&
            headerValue is string correlationIdString &&
            Guid.TryParse(correlationIdString, out var correlationId))
        {
            return correlationId;
        }

        // Try to extract from MassTransit's built-in correlation ID
        if (context.CorrelationId.HasValue)
        {
            return context.CorrelationId.Value;
        }

        // Try to extract from Activity baggage
        var activity = Activity.Current;
        if (activity?.GetBaggageItem("correlation.id") is string baggageValue &&
            Guid.TryParse(baggageValue, out var baggageCorrelationId))
        {
            return baggageCorrelationId;
        }

        return Guid.Empty;
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("correlationId");
    }
}
