using Shared.Models;

namespace Shared.Processor.Services;

/// <summary>
/// Service interface for exposing ProcessorHealthCacheEntry properties as OpenTelemetry metrics.
/// Provides comprehensive processor health and performance metrics for analysis and monitoring.
/// </summary>
public interface IProcessorHealthMetricsService
{
    /// <summary>
    /// Records all metrics from a ProcessorHealthCacheEntry.
    /// This method extracts and publishes all relevant metrics from the health cache entry.
    /// </summary>
    /// <param name="healthEntry">The processor health cache entry containing metrics data</param>
    void RecordHealthCacheEntryMetrics(ProcessorHealthCacheEntry healthEntry);

    /// <summary>
    /// Records processor status metrics.
    /// </summary>
    /// <param name="processorId">Unique processor identifier</param>
    /// <param name="status">Current health status</param>
    /// <param name="processorName">Name of the processor from configuration</param>
    /// <param name="processorVersion">Version of the processor from configuration</param>
    void RecordProcessorStatus(Guid processorId, HealthStatus status, string processorName, string processorVersion);

    /// <summary>
    /// Records processor uptime metrics.
    /// </summary>
    /// <param name="processorId">Unique processor identifier</param>
    /// <param name="uptime">Current processor uptime</param>
    /// <param name="processorName">Name of the processor from configuration</param>
    /// <param name="processorVersion">Version of the processor from configuration</param>
    void RecordProcessorUptime(Guid processorId, TimeSpan uptime, string processorName, string processorVersion);

    /// <summary>
    /// Records processor performance metrics.
    /// </summary>
    /// <param name="processorId">Unique processor identifier</param>
    /// <param name="performanceMetrics">Performance metrics data</param>
    /// <param name="processorName">Name of the processor from configuration</param>
    /// <param name="processorVersion">Version of the processor from configuration</param>
    void RecordPerformanceMetrics(Guid processorId, ProcessorPerformanceMetrics performanceMetrics, string processorName, string processorVersion);

    /// <summary>
    /// Records processor metadata metrics.
    /// </summary>
    /// <param name="processorId">Unique processor identifier</param>
    /// <param name="metadata">Processor metadata</param>
    /// <param name="processorName">Name of the processor from configuration</param>
    /// <param name="processorVersion">Version of the processor from configuration</param>
    void RecordProcessorMetadata(Guid processorId, ProcessorMetadata metadata, string processorName, string processorVersion);

    /// <summary>
    /// Records health check results metrics.
    /// </summary>
    /// <param name="processorId">Unique processor identifier</param>
    /// <param name="healthChecks">Health check results</param>
    /// <param name="processorName">Name of the processor from configuration</param>
    /// <param name="processorVersion">Version of the processor from configuration</param>
    void RecordHealthCheckResults(Guid processorId, Dictionary<string, HealthCheckResult> healthChecks, string processorName, string processorVersion);

    /// <summary>
    /// Records aggregated cache metrics.
    /// </summary>
    /// <param name="processorId">Unique processor identifier</param>
    /// <param name="averageEntryAge">Average age of cache entries in seconds</param>
    /// <param name="activeEntries">Number of active cache entries</param>
    /// <param name="processorName">Name of the processor from configuration</param>
    /// <param name="processorVersion">Version of the processor from configuration</param>
    void RecordCacheMetrics(Guid processorId, double averageEntryAge, long activeEntries, string processorName, string processorVersion);

    /// <summary>
    /// Records exception metrics for processor monitoring.
    /// </summary>
    /// <param name="exceptionType">Type of exception (e.g., ValidationException, ProcessingException)</param>
    /// <param name="severity">Severity level (warning, error, critical)</param>
    /// <param name="isCritical">Whether this exception affects processor operation</param>
    void RecordException(string exceptionType, string severity, bool isCritical = false);



}
