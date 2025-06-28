using System.Threading.Tasks;
using MassTransit;
using Shared.Correlation;
using Microsoft.Extensions.Logging;

namespace Shared.MassTransit;

/// <summary>
/// MassTransit publish filter that automatically adds correlation ID headers to outgoing messages.
/// </summary>
public class CorrelationIdPublishFilter<T> : IFilter<PublishContext<T>>
    where T : class
{
    private readonly ICorrelationIdContext _correlationIdContext;
    private readonly ILogger<CorrelationIdPublishFilter<T>> _logger;

    public CorrelationIdPublishFilter(
        ICorrelationIdContext correlationIdContext,
        ILogger<CorrelationIdPublishFilter<T>> logger)
    {
        _correlationIdContext = correlationIdContext;
        _logger = logger;
    }

    public async Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        var correlationId = _correlationIdContext.Current;
        
        if (correlationId != System.Guid.Empty)
        {
            // Set correlation ID in message headers
            context.Headers.Set("X-Correlation-ID", correlationId.ToString());
            
            _logger.LogDebugWithCorrelation("Added correlation ID to outgoing message: {CorrelationId} for message type {MessageType}", 
                correlationId, typeof(T).Name);
        }
        else
        {
            _logger.LogWarningWithCorrelation("No correlation ID available for outgoing message of type {MessageType}", typeof(T).Name);
        }

        await next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("correlationId");
    }
}

/// <summary>
/// MassTransit send filter that automatically adds correlation ID headers to outgoing messages.
/// </summary>
public class CorrelationIdSendFilter<T> : IFilter<SendContext<T>>
    where T : class
{
    private readonly ICorrelationIdContext _correlationIdContext;
    private readonly ILogger<CorrelationIdSendFilter<T>> _logger;

    public CorrelationIdSendFilter(
        ICorrelationIdContext correlationIdContext,
        ILogger<CorrelationIdSendFilter<T>> logger)
    {
        _correlationIdContext = correlationIdContext;
        _logger = logger;
    }

    public async Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        var correlationId = _correlationIdContext.Current;
        
        if (correlationId != System.Guid.Empty)
        {
            // Set correlation ID in message headers
            context.Headers.Set("X-Correlation-ID", correlationId.ToString());
            
            _logger.LogDebugWithCorrelation("Added correlation ID to outgoing message: {CorrelationId} for message type {MessageType}", 
                correlationId, typeof(T).Name);
        }
        else
        {
            _logger.LogWarningWithCorrelation("No correlation ID available for outgoing message of type {MessageType}", typeof(T).Name);
        }

        await next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("correlationId");
    }
}
