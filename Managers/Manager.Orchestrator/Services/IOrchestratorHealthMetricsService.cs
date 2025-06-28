using Shared.Models;

namespace Manager.Orchestrator.Services;

/// <summary>
/// Service for recording orchestrator health metrics for monitoring system health, performance, cache operations, and exceptions.
/// Provides health-focused metrics for orchestrator operations following the processor health metrics design pattern.
/// </summary>
public interface IOrchestratorHealthMetricsService : IDisposable
{


    // Cache Operations Metrics

    /// <summary>
    /// Records cache operation metrics (optimized for anomaly detection)
    /// </summary>
    /// <param name="success">Whether the cache operation was successful</param>
    /// <param name="operationType">Type of cache operation (e.g., "read", "write", "delete")</param>
    void RecordCacheOperation(bool success, string operationType);

    /// <summary>
    /// Updates the current number of active cache entries
    /// </summary>
    /// <param name="count">Current active cache entry count</param>
    void UpdateActiveCacheEntries(long count);

    // Cache Metrics (following processor pattern)

    /// <summary>
    /// Records aggregated cache metrics (following processor pattern)
    /// </summary>
    /// <param name="averageEntryAge">Average age of cache entries in seconds</param>
    /// <param name="activeEntries">Number of active cache entries</param>
    void RecordCacheMetrics(double averageEntryAge, long activeEntries);

    // Performance Metrics (following processor pattern)

    /// <summary>
    /// Records orchestrator performance metrics (following processor pattern)
    /// </summary>
    /// <param name="cpuUsagePercent">Current CPU usage percentage (0-100)</param>
    /// <param name="memoryUsageBytes">Current memory usage in bytes</param>
    void RecordPerformanceMetrics(double cpuUsagePercent, long memoryUsageBytes);

    // Status and Health Metrics (following processor pattern)

    /// <summary>
    /// Records orchestrator health status (following processor pattern)
    /// </summary>
    /// <param name="status">Health status (0=Healthy, 1=Degraded, 2=Unhealthy)</param>
    void RecordOrchestratorStatus(int status);

    /// <summary>
    /// Records orchestrator uptime metrics (following processor pattern)
    /// </summary>
    void RecordOrchestratorUptime();

    /// <summary>
    /// Records health check metrics (optimized for anomaly detection)
    /// </summary>
    /// <param name="healthCheckName">Name of the health check</param>
    /// <param name="healthCheckStatus">Status of the health check</param>
    void RecordHealthCheck(string healthCheckName, string healthCheckStatus);

    /// <summary>
    /// Records health check results metrics (following processor pattern)
    /// </summary>
    /// <param name="healthChecks">Health check results from health report</param>
    void RecordHealthCheckResults(Dictionary<string, HealthCheckResult> healthChecks);

    // Metadata Metrics

    /// <summary>
    /// Records orchestrator metadata metrics (following processor pattern)
    /// </summary>
    /// <param name="processId">Process ID of the orchestrator</param>
    /// <param name="startTime">Start time of the orchestrator</param>
    void RecordOrchestratorMetadata(int processId, DateTime startTime);

    // Exception Metrics

    /// <summary>
    /// Records exception metrics for orchestrator monitoring.
    /// </summary>
    /// <param name="exceptionType">Type of exception (e.g., ValidationException, ProcessingException)</param>
    /// <param name="severity">Severity level (warning, error, critical)</param>
    /// <param name="isCritical">Whether this exception affects orchestrator operation</param>
    void RecordException(string exceptionType, string severity, bool isCritical = false);


}
