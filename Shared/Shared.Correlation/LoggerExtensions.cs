using Microsoft.Extensions.Logging;

namespace Shared.Correlation;

/// <summary>
/// Extension methods for ILogger that automatically include correlation IDs in log statements.
/// Provides structured logging with consistent correlation ID inclusion.
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Logs a debug message with automatic correlation ID inclusion.
    /// </summary>
    public static void LogDebugWithCorrelation(this ILogger logger, string message, params object?[] args)
    {
        LogWithCorrelation(logger, LogLevel.Debug, message, args);
    }

    /// <summary>
    /// Logs a debug message with automatic correlation ID inclusion.
    /// </summary>
    public static void LogDebugWithCorrelation(this ILogger logger, Exception ex, params object?[] args)
    {
        LogWithCorrelation(logger, LogLevel.Debug, ex.Message, args);
    }
    /// <summary>
    /// Logs an information message with automatic correlation ID inclusion.
    /// </summary>
    public static void LogInformationWithCorrelation(this ILogger logger, string message, params object?[] args)
    {
        LogWithCorrelation(logger, LogLevel.Information, message, args);
    }

    /// <summary>
    /// Logs a warning message with automatic correlation ID inclusion.
    /// </summary>
    public static void LogWarningWithCorrelation(this ILogger logger, string message, params object?[] args)
    {
        LogWithCorrelation(logger, LogLevel.Warning, message, args);
    }

    /// <summary>
    /// Logs a warning message with automatic correlation ID inclusion.
    /// </summary>
    public static void LogWarningWithCorrelation(this ILogger logger, Exception ex, params object?[] args)
    {
        LogWithCorrelation(logger, LogLevel.Warning, ex.Message, args);
    }

    /// <summary>
    /// Logs an error message with automatic correlation ID inclusion.
    /// </summary>
    public static void LogErrorWithCorrelation(this ILogger logger, string message, params object?[] args)
    {
        LogWithCorrelation(logger, LogLevel.Error, message, args);
    }

    /// <summary>
    /// Logs an error message with exception and automatic correlation ID inclusion.
    /// </summary>
    public static void LogErrorWithCorrelation(this ILogger logger, Exception exception, string message, params object?[] args)
    {
        LogWithCorrelation(logger, LogLevel.Error, exception, message, args);
    }

    /// <summary>
    /// Logs a critical message with automatic correlation ID inclusion.
    /// </summary>
    public static void LogCriticalWithCorrelation(this ILogger logger, string message, params object?[] args)
    {
        LogWithCorrelation(logger, LogLevel.Critical, message, args);
    }

    /// <summary>
    /// Logs a critical message with exception and automatic correlation ID inclusion.
    /// </summary>
    public static void LogCriticalWithCorrelation(this ILogger logger, Exception exception, string message, params object?[] args)
    {
        LogWithCorrelation(logger, LogLevel.Critical, exception, message, args);
    }

    /// <summary>
    /// Creates a logging scope with correlation ID for automatic inclusion in all log statements within the scope.
    /// </summary>
    public static IDisposable? BeginCorrelationScope(this ILogger logger, Guid? correlationId = null)
    {
        var actualCorrelationId = correlationId ?? GetCurrentCorrelationId();
        return logger.BeginScope(new { CorrelationId = actualCorrelationId });
    }

    private static void LogWithCorrelation(ILogger logger, LogLevel logLevel, string message, params object?[] args)
    {
        LogWithCorrelation(logger, logLevel, null, message, args);
    }

    private static void LogWithCorrelation(ILogger logger, LogLevel logLevel, Exception? exception, string message, params object?[] args)
    {
        if (!logger.IsEnabled(logLevel))
            return;

        var correlationId = GetCurrentCorrelationId();

        // Enhance the message template to include correlation ID
        var enhancedMessage = $"[CorrelationId: {{CorrelationId}}] {message}";

        // Create enhanced args array with correlation ID as first parameter
        // Handle nullable args by converting null values to "<null>" for logging
        var enhancedArgs = new object[args.Length + 1];
        enhancedArgs[0] = correlationId;
        for (int i = 0; i < args.Length; i++)
        {
            enhancedArgs[i + 1] = args[i] ?? "<null>";
        }

        if (exception != null)
        {
            logger.Log(logLevel, exception, enhancedMessage, enhancedArgs);
        }
        else
        {
            logger.Log(logLevel, enhancedMessage, enhancedArgs);
        }
    }

    private static Guid GetCurrentCorrelationId()
    {
        // Use the same logic as CorrelationIdContext to get correlation ID
        // First check AsyncLocal storage, then fall back to Activity baggage
        return CorrelationIdContext.GetCurrentCorrelationIdStatic();
    }
}


