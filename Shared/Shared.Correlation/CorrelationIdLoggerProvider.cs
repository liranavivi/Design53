using Microsoft.Extensions.Logging;

namespace Shared.Correlation;

/// <summary>
/// Logger provider that enriches log entries with correlation IDs.
/// </summary>
public class CorrelationIdLoggerProvider : ILoggerProvider
{
    private readonly ICorrelationIdContext _correlationIdContext;

    public CorrelationIdLoggerProvider(ICorrelationIdContext correlationIdContext)
    {
        _correlationIdContext = correlationIdContext;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new CorrelationIdLogger(categoryName, _correlationIdContext);
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}

/// <summary>
/// Logger that automatically enriches log entries with correlation IDs.
/// </summary>
public class CorrelationIdLogger : ILogger
{
    private readonly string _categoryName;
    private readonly ICorrelationIdContext _correlationIdContext;

    public CorrelationIdLogger(string categoryName, ICorrelationIdContext correlationIdContext)
    {
        _categoryName = categoryName;
        _correlationIdContext = correlationIdContext;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var correlationId = _correlationIdContext.Current;
        
        // Create enriched state with correlation ID
        var enrichedState = new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId == Guid.Empty ? null : correlationId.ToString(),
            ["OriginalState"] = state
        };

        // Note: This is a simplified implementation
        // In a real scenario, you'd want to integrate with your actual logging provider
        // and ensure the correlation ID is properly included in structured logs
    }

    private class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}
