using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using Shared.Models;
using Shared.Extensions;
using Shared.Correlation;

namespace Manager.Orchestrator.Services;

/// <summary>
/// Service for recording orchestrator health metrics for monitoring system health, performance, cache operations, and exceptions.
/// Provides health-focused metrics for orchestrator operations following the processor health metrics design pattern.
/// Uses IOrchestratorMetricsLabelService for consistent labeling across all metrics.
/// </summary>
public class OrchestratorHealthMetricsService : IOrchestratorHealthMetricsService, IDisposable
{
    private readonly ManagerConfiguration _config;
    private readonly ILogger<OrchestratorHealthMetricsService> _logger;
    private readonly IOrchestratorMetricsLabelService _labelService;
    private readonly Meter _meter;



    // Performance Metrics (following processor pattern)
    private readonly Gauge<double> _cpuUsageGauge;
    private readonly Gauge<long> _memoryUsageGauge;

    // Cache Metrics (aggregated) - following processor pattern
    private readonly Gauge<double> _cacheAverageEntryAgeGauge;
    private readonly Gauge<long> _cacheActiveEntriesGauge;

    // Cache Operations Metrics (orchestrator-specific)
    private readonly Counter<long> _cacheOperationsCounter;

    // Status and Health Metrics (following processor pattern)
    private readonly Gauge<int> _orchestratorStatusGauge;
    private readonly Gauge<double> _orchestratorUptimeGauge;
    private readonly Counter<long> _healthCheckCounter;

    // Metadata Metrics (following processor pattern)
    private readonly Gauge<int> _orchestratorProcessIdGauge;
    private readonly Counter<long> _orchestratorStartCounter;

    // Exception Metrics
    private readonly Counter<long> _exceptionsCounter;
    private readonly Counter<long> _criticalExceptionsCounter;



    // Track recorded start times to prevent duplicate start events
    private readonly HashSet<string> _recordedStartTimes = new();

    // Start time tracking for uptime calculation
    private readonly DateTime _startTime;

    public OrchestratorHealthMetricsService(
        IOptions<ManagerConfiguration> config,
        ILogger<OrchestratorHealthMetricsService> logger,
        IOrchestratorMetricsLabelService labelService)
    {
        _config = config.Value;
        _logger = logger;
        _labelService = labelService;
        _startTime = DateTime.UtcNow;

        // Use the recommended unique meter name pattern: {Version}_{Name}
        var meterName = $"{_config.Version}_{_config.Name}";
        var fullMeterName = $"{meterName}.HealthMetrics";

        _logger.LogInformationWithCorrelation(
            "🔍 TRACE: Creating meter - OrchestratorName: {OrchestratorName}, Version: {OrchestratorVersion}, MeterName: {MeterName}",
            _config.Name, _config.Version, fullMeterName);

        _meter = new Meter(fullMeterName);

        _logger.LogInformationWithCorrelation(
            "✅ TRACE: Meter created successfully - Name: {MeterName}, Version: {MeterVersion}",
            _meter.Name, _meter.Version);

        // Initialize all metrics with descriptive names and units
        (_cpuUsageGauge, _memoryUsageGauge) = InitializePerformanceMetrics();
        (_cacheAverageEntryAgeGauge, _cacheActiveEntriesGauge, _cacheOperationsCounter) = InitializeCacheMetrics();
        (_orchestratorStatusGauge, _orchestratorUptimeGauge, _healthCheckCounter) = InitializeHealthMetrics();
        (_orchestratorProcessIdGauge, _orchestratorStartCounter) = InitializeMetadataMetrics();
        (_exceptionsCounter, _criticalExceptionsCounter) = InitializeExceptionMetrics();

        _logger.LogDebugWithCorrelation(
            "🔥 DEBUG: OrchestratorHealthMetricsService fully initialized with health metrics");

        _logger.LogInformationWithCorrelation(
            "OrchestratorHealthMetricsService initialized with meter name: {MeterName}, Composite Key: {CompositeKey}",
            fullMeterName, _labelService.OrchestratorCompositeKey);
    }







    private (Gauge<double>, Gauge<long>) InitializePerformanceMetrics()
    {
        var cpuUsageGauge = _meter.CreateGauge<double>(
            "orchestrator_cpu_usage_percent",
            "Current CPU usage percentage (0-100)");

        var memoryUsageGauge = _meter.CreateGauge<long>(
            "orchestrator_memory_usage_bytes",
            "Current memory usage in bytes");

        return (cpuUsageGauge, memoryUsageGauge);
    }

    private (Gauge<double>, Gauge<long>, Counter<long>) InitializeCacheMetrics()
    {
        var cacheAverageEntryAgeGauge = _meter.CreateGauge<double>(
            "orchestrator_cache_average_entry_age_seconds",
            "Average age of cache entries in seconds");

        var cacheActiveEntriesGauge = _meter.CreateGauge<long>(
            "orchestrator_cache_active_entries_total",
            "Total number of active cache entries");

        var cacheOperationsCounter = _meter.CreateCounter<long>(
            "orchestrator_cache_operations_total",
            "Total number of cache operations performed");

        return (cacheAverageEntryAgeGauge, cacheActiveEntriesGauge, cacheOperationsCounter);
    }

    private (Gauge<int>, Gauge<double>, Counter<long>) InitializeHealthMetrics()
    {
        _logger.LogInformationWithCorrelation("🔍 TRACE: Creating orchestrator_health_status gauge...");
        var orchestratorStatusGauge = _meter.CreateGauge<int>(
            "orchestrator_health_status",
            "Current health status of the orchestrator (0=Healthy, 1=Degraded, 2=Unhealthy)");
        _logger.LogInformationWithCorrelation("✅ TRACE: orchestrator_health_status gauge created - Meter: {MeterName}", _meter.Name);

        _logger.LogInformationWithCorrelation("🔍 TRACE: Creating orchestrator_uptime_seconds gauge...");
        var orchestratorUptimeGauge = _meter.CreateGauge<double>(
            "orchestrator_uptime_seconds",
            "Total uptime of the orchestrator in seconds");
        _logger.LogInformationWithCorrelation("✅ TRACE: orchestrator_uptime_seconds gauge created - Meter: {MeterName}", _meter.Name);

        _logger.LogInformationWithCorrelation("🔍 TRACE: Creating orchestrator_health_checks_total counter...");
        var healthCheckCounter = _meter.CreateCounter<long>(
            "orchestrator_health_checks_total",
            "Total number of health checks performed");
        _logger.LogInformationWithCorrelation("✅ TRACE: orchestrator_health_checks_total counter created - Meter: {MeterName}", _meter.Name);

        return (orchestratorStatusGauge, orchestratorUptimeGauge, healthCheckCounter);
    }

    private (Gauge<int>, Counter<long>) InitializeMetadataMetrics()
    {
        var orchestratorProcessIdGauge = _meter.CreateGauge<int>(
            "orchestrator_process_id",
            "Process ID of the orchestrator");

        var orchestratorStartCounter = _meter.CreateCounter<long>(
            "orchestrator_starts_total",
            "Total number of times the orchestrator has been started");

        return (orchestratorProcessIdGauge, orchestratorStartCounter);
    }

    private (Counter<long>, Counter<long>) InitializeExceptionMetrics()
    {
        var exceptionsCounter = _meter.CreateCounter<long>(
            "orchestrator_exceptions_total",
            "Total number of exceptions thrown by the orchestrator");

        var criticalExceptionsCounter = _meter.CreateCounter<long>(
            "orchestrator_critical_exceptions_total",
            "Total number of critical exceptions that affect orchestrator operation");

        _logger.LogInformationWithCorrelation(
            "✅ TRACE: Exception metrics initialized - orchestrator_exceptions_total and orchestrator_critical_exceptions_total");

        return (exceptionsCounter, criticalExceptionsCounter);
    }









    // Cache Operations Methods

    public void RecordCacheOperation(bool success, string operationType)
    {
        var tags = _labelService.GetCacheLabels(operationType, success ? "success" : "failed");

        _cacheOperationsCounter.Add(1, tags);

        _logger.LogDebugWithCorrelation(
            "Recorded cache operation metrics: OperationType={OperationType}, Success={Success}",
            operationType, success);
    }

    public void UpdateActiveCacheEntries(long count)
    {
        var tags = _labelService.GetSystemLabels();
        _cacheActiveEntriesGauge.Record(count, tags);

        _logger.LogDebugWithCorrelation(
            "Updated active cache entries: Count={Count}",
            count);
    }

    public void RecordCacheMetrics(double averageEntryAge, long activeEntries)
    {
        var tags = _labelService.GetCacheLabels();

        _cacheAverageEntryAgeGauge.Record(averageEntryAge, tags);
        _cacheActiveEntriesGauge.Record(activeEntries, tags);

        _logger.LogDebugWithCorrelation(
            "🔥 DEBUG: Recorded cache metrics - AverageAge: {AverageAge}s, ActiveEntries: {ActiveEntries}, CompositeKey: {CompositeKey}",
            averageEntryAge, activeEntries, _labelService.OrchestratorCompositeKey);
    }

    // Performance Methods

    public void RecordPerformanceMetrics(double cpuUsagePercent, long memoryUsageBytes)
    {
        var tags = _labelService.GetSystemLabels();

        // Record current resource usage as gauges
        _cpuUsageGauge.Record(cpuUsagePercent, tags);
        _memoryUsageGauge.Record(memoryUsageBytes, tags);

        _logger.LogDebugWithCorrelation(
            "🔥 DEBUG: Recorded performance metrics - CPU: {CpuUsage}%, Memory: {MemoryUsage} bytes, CompositeKey: {CompositeKey}",
            cpuUsagePercent, memoryUsageBytes, _labelService.OrchestratorCompositeKey);
    }

    // Health Methods

    public void RecordOrchestratorStatus(int status)
    {
        _logger.LogInformationWithCorrelation("🔍 TRACE: Getting system labels from label service...");
        var tags = _labelService.GetSystemLabels();
        _logger.LogInformationWithCorrelation("✅ TRACE: Got {TagCount} labels: {Labels}", tags.Length, string.Join(", ", tags.Select(t => $"{t.Key}={t.Value}")));

        _logger.LogInformationWithCorrelation("🔍 TRACE: Recording orchestrator_health_status gauge with value {StatusValue}...", status);
        _orchestratorStatusGauge.Record(status, tags);
        _logger.LogInformationWithCorrelation("✅ TRACE: orchestrator_health_status gauge recorded successfully");

        _logger.LogDebugWithCorrelation(
            "🔥 DEBUG: Recorded orchestrator_health_status metric - Status: {Status}, CompositeKey: {CompositeKey}",
            status, _labelService.OrchestratorCompositeKey);
    }

    public void RecordOrchestratorUptime()
    {
        _logger.LogInformationWithCorrelation("🔍 TRACE: Getting system labels for uptime metric...");
        var tags = _labelService.GetSystemLabels();
        _logger.LogInformationWithCorrelation("✅ TRACE: Got {TagCount} labels for uptime", tags.Length);

        var uptime = DateTime.UtcNow - _startTime;

        _logger.LogInformationWithCorrelation("🔍 TRACE: Recording orchestrator_uptime_seconds gauge with value {UptimeSeconds}...", uptime.TotalSeconds);
        _orchestratorUptimeGauge.Record(uptime.TotalSeconds, tags);
        _logger.LogInformationWithCorrelation("✅ TRACE: orchestrator_uptime_seconds gauge recorded successfully");

        _logger.LogDebugWithCorrelation(
            "🔥 DEBUG: Recorded orchestrator_uptime_seconds metric - Uptime: {Uptime} ({UptimeSeconds}s), CompositeKey: {CompositeKey}",
            uptime, uptime.TotalSeconds, _labelService.OrchestratorCompositeKey);
    }

    public void RecordHealthCheck(string healthCheckName, string healthCheckStatus)
    {
        var tags = _labelService.GetHealthLabels(healthCheckName, healthCheckStatus);

        _healthCheckCounter.Add(1, tags);

        _logger.LogDebugWithCorrelation(
            "Recorded health check metrics: HealthCheckName={HealthCheckName}, Status={Status}",
            healthCheckName, healthCheckStatus);
    }

    public void RecordHealthCheckResults(Dictionary<string, HealthCheckResult> healthChecks)
    {
        foreach (var healthCheck in healthChecks)
        {
            var tags = _labelService.GetHealthLabels(healthCheck.Key, healthCheck.Value.Status.ToString());
            _healthCheckCounter.Add(1, tags);
        }

        _logger.LogDebugWithCorrelation(
            "🔥 DEBUG: Recorded {HealthCheckCount} health check results metrics, CompositeKey: {CompositeKey}",
            healthChecks.Count, _labelService.OrchestratorCompositeKey);
    }

    // Metadata Methods

    public void RecordOrchestratorMetadata(int processId, DateTime startTime)
    {
        var tags = _labelService.GetSystemLabels().ToList();
        tags.Add(new KeyValuePair<string, object?>("host_name", Environment.MachineName));

        _orchestratorProcessIdGauge.Record(processId, tags.ToArray());

        // Record orchestrator start event (only once per start time)
        var startTimeKey = $"{_labelService.OrchestratorInstanceId}_{startTime:yyyy-MM-ddTHH:mm:ssZ}";

        lock (_recordedStartTimes)
        {
            if (!_recordedStartTimes.Contains(startTimeKey))
            {
                _recordedStartTimes.Add(startTimeKey);

                var startTags = _labelService.GetSystemLabels().ToList();
                startTags.Add(new KeyValuePair<string, object?>("start_time", startTime.ToString("yyyy-MM-ddTHH:mm:ssZ")));

                _orchestratorStartCounter.Add(1, startTags.ToArray());

                _logger.LogDebugWithCorrelation(
                    "Recorded orchestrator start event for InstanceId: {InstanceId}, StartTime: {StartTime}, CompositeKey: {CompositeKey}",
                    _labelService.OrchestratorInstanceId, startTime, _labelService.OrchestratorCompositeKey);
            }
        }

        _logger.LogDebugWithCorrelation(
            "Recorded orchestrator metadata - ProcessId: {ProcessId}, CompositeKey: {CompositeKey}",
            processId, _labelService.OrchestratorCompositeKey);
    }

    // Exception Methods

    /// <summary>
    /// Records exception metrics for orchestrator monitoring.
    /// </summary>
    /// <param name="exceptionType">Type of exception (e.g., ValidationException, ProcessingException)</param>
    /// <param name="severity">Severity level (warning, error, critical)</param>
    /// <param name="isCritical">Whether this exception affects orchestrator operation</param>
    public void RecordException(string exceptionType, string severity, bool isCritical = false)
    {
        var tags = _labelService.GetSystemLabels().ToList();
        tags.Add(new KeyValuePair<string, object?>("exception_type", exceptionType));
        tags.Add(new KeyValuePair<string, object?>("severity", severity));

        _exceptionsCounter.Add(1, tags.ToArray());

        if (isCritical)
        {
            _criticalExceptionsCounter.Add(1, tags.ToArray());
        }

        _logger.LogDebugWithCorrelation(
            "🔥 DEBUG: Recorded exception metric - Type: {ExceptionType}, Severity: {Severity}, Critical: {IsCritical}, CompositeKey: {CompositeKey}",
            exceptionType, severity, isCritical, _labelService.OrchestratorCompositeKey);
    }



    public void Dispose()
    {
        _meter?.Dispose();
        _logger.LogInformationWithCorrelation("OrchestratorHealthMetricsService disposed");
    }
}
