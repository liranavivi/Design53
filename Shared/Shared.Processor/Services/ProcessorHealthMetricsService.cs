using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Correlation;
using Shared.Models;
using Shared.Processor.Models;
using System.Diagnostics.Metrics;

namespace Shared.Processor.Services;

/// <summary>
/// Service for exposing ProcessorHealthCacheEntry properties as OpenTelemetry metrics.
/// Provides comprehensive processor health and performance metrics for analysis and monitoring.
/// Uses consistent labeling from appsettings configuration (Name and Version).
/// </summary>
public class ProcessorHealthMetricsService : IProcessorHealthMetricsService
{
    private readonly ProcessorConfiguration _config;
    private readonly ILogger<ProcessorHealthMetricsService> _logger;
    private readonly Meter _meter;

    // Track recorded start times to prevent duplicate start events
    private readonly HashSet<string> _recordedStartTimes = new();

    // Status and Health Metrics
    private readonly Gauge<int> _processorStatusGauge;
    private readonly Gauge<double> _processorUptimeGauge;
    private readonly Counter<long> _healthCheckCounter;

    // Performance Metrics
    private readonly Gauge<double> _cpuUsageGauge;
    private readonly Gauge<long> _memoryUsageGauge;

    // Cache Metrics (aggregated)
    private readonly Gauge<double> _cacheAverageEntryAgeGauge;
    private readonly Gauge<long> _cacheActiveEntriesGauge;

    // Metadata Metrics
    private readonly Gauge<int> _processorProcessIdGauge;
    private readonly Counter<long> _processorStartCounter;

    // Exception Metrics
    private readonly Counter<long> _exceptionsCounter;
    private readonly Counter<long> _criticalExceptionsCounter;

    private readonly IProcessorMetricsLabelService _labelService;

    public ProcessorHealthMetricsService(
        IOptions<ProcessorConfiguration> config,
        IConfiguration configuration,
        ILogger<ProcessorHealthMetricsService> logger,
        IProcessorMetricsLabelService labelService)
    {
        _config = config.Value;
        _logger = logger;
        _labelService = labelService;

        // Use the recommended unique meter name pattern: {Version}_{Name}
        var meterName = $"{_config.Version}_{_config.Name}";
        var fullMeterName = $"{meterName}.HealthMetrics";

        _logger.LogInformationWithCorrelation(
            "🔍 TRACE: Creating meter - ProcessorName: {ProcessorName}, Version: {ProcessorVersion}, MeterName: {MeterName}",
            _config.Name, _config.Version, fullMeterName);

        _meter = new Meter(fullMeterName);

        _logger.LogInformationWithCorrelation(
            "✅ TRACE: Meter created successfully - Name: {MeterName}, Version: {MeterVersion}",
            _meter.Name, _meter.Version);

        // Initialize all metrics with descriptive names and units
        (_processorStatusGauge, _processorUptimeGauge, _healthCheckCounter) = InitializeStatusMetrics();
        (_cpuUsageGauge, _memoryUsageGauge) = InitializePerformanceMetrics();
        (_cacheAverageEntryAgeGauge, _cacheActiveEntriesGauge) = InitializeCacheMetrics();
        (_processorProcessIdGauge, _processorStartCounter) = InitializeMetadataMetrics();
        (_exceptionsCounter, _criticalExceptionsCounter) = InitializeExceptionMetrics();

        _logger.LogDebugWithCorrelation(
            "🔥 DEBUG: ProcessorHealthMetricsService fully initialized with all meters and metrics");
    }

    private (Gauge<int>, Gauge<double>, Counter<long>) InitializeStatusMetrics()
    {
        _logger.LogInformationWithCorrelation("🔍 TRACE: Creating processor_health_status gauge...");
        var processorStatusGauge = _meter.CreateGauge<int>(
            "processor_health_status",
            "Current health status of the processor (0=Healthy, 1=Degraded, 2=Unhealthy)");
        _logger.LogInformationWithCorrelation("✅ TRACE: processor_health_status gauge created - Meter: {MeterName}", _meter.Name);

        _logger.LogInformationWithCorrelation("🔍 TRACE: Creating processor_uptime_seconds gauge...");
        var processorUptimeGauge = _meter.CreateGauge<double>(
            "processor_uptime_seconds",
            "Total uptime of the processor in seconds");
        _logger.LogInformationWithCorrelation("✅ TRACE: processor_uptime_seconds gauge created - Meter: {MeterName}", _meter.Name);

        _logger.LogInformationWithCorrelation("🔍 TRACE: Creating processor_health_checks_total counter...");
        var healthCheckCounter = _meter.CreateCounter<long>(
            "processor_health_checks_total",
            "Total number of health checks performed");
        _logger.LogInformationWithCorrelation("✅ TRACE: processor_health_checks_total counter created - Meter: {MeterName}", _meter.Name);

        return (processorStatusGauge, processorUptimeGauge, healthCheckCounter);
    }

    private (Gauge<double>, Gauge<long>) InitializePerformanceMetrics()
    {
        var cpuUsageGauge = _meter.CreateGauge<double>(
            "processor_cpu_usage_percent",
            "Current CPU usage percentage (0-100)");

        var memoryUsageGauge = _meter.CreateGauge<long>(
            "processor_memory_usage_bytes",
            "Current memory usage in bytes");

        return (cpuUsageGauge, memoryUsageGauge);
    }

    private (Gauge<double>, Gauge<long>) InitializeCacheMetrics()
    {
        var cacheAverageEntryAgeGauge = _meter.CreateGauge<double>(
            "processor_cache_average_entry_age_seconds",
            "Average age of cache entries in seconds");

        var cacheActiveEntriesGauge = _meter.CreateGauge<long>(
            "processor_cache_active_entries_total",
            "Total number of active cache entries");

        return (cacheAverageEntryAgeGauge, cacheActiveEntriesGauge);
    }

    private (Gauge<int>, Counter<long>) InitializeMetadataMetrics()
    {
        var processorProcessIdGauge = _meter.CreateGauge<int>(
            "processor_process_id",
            "Process ID of the processor");

        var processorStartCounter = _meter.CreateCounter<long>(
            "processor_starts_total",
            "Total number of times the processor has been started");

        return (processorProcessIdGauge, processorStartCounter);
    }

    private (Counter<long>, Counter<long>) InitializeExceptionMetrics()
    {
        var exceptionsCounter = _meter.CreateCounter<long>(
            "processor_exceptions_total",
            "Total number of exceptions thrown by the processor");

        var criticalExceptionsCounter = _meter.CreateCounter<long>(
            "processor_critical_exceptions_total",
            "Total number of critical exceptions that affect processor operation");

        _logger.LogInformationWithCorrelation(
            "✅ TRACE: Exception metrics initialized - processor_exceptions_total and processor_critical_exceptions_total");

        return (exceptionsCounter, criticalExceptionsCounter);
    }

    public void RecordHealthCacheEntryMetrics(ProcessorHealthCacheEntry healthEntry)
    {
        try
        {
            var processorName = _config.Name;
            var processorVersion = _config.Version;

            _logger.LogDebugWithCorrelation(
                "🔥 DEBUG: RecordHealthCacheEntryMetrics called for ProcessorId: {ProcessorId}, Status: {Status}, ProcessorName: {ProcessorName}, Version: {ProcessorVersion}",
                healthEntry.ProcessorId, healthEntry.Status, processorName, processorVersion);

            // Record all metrics from the health cache entry - ALWAYS record regardless of ProcessorId
            RecordProcessorStatus(healthEntry.ProcessorId, healthEntry.Status, processorName, processorVersion);
            RecordProcessorUptime(healthEntry.ProcessorId, healthEntry.Uptime, processorName, processorVersion);
            RecordPerformanceMetrics(healthEntry.ProcessorId, healthEntry.PerformanceMetrics, processorName, processorVersion);
            RecordProcessorMetadata(healthEntry.ProcessorId, healthEntry.Metadata, processorName, processorVersion);
            RecordHealthCheckResults(healthEntry.ProcessorId, healthEntry.HealthChecks, processorName, processorVersion);

            _logger.LogDebugWithCorrelation(
                "🔥 DEBUG: Recorded processor status ({Status}) and uptime ({Uptime}) metrics",
                healthEntry.Status, healthEntry.Uptime);

            // Calculate cache metrics from the entry
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var entryAge = now - healthEntry.LastUpdated;
            RecordCacheMetrics(healthEntry.ProcessorId, entryAge, 1, processorName, processorVersion);

            _logger.LogDebugWithCorrelation(
                "🔥 DEBUG: Successfully recorded ALL health cache entry metrics for processor {ProcessorId} ({ProcessorName} v{ProcessorVersion})",
                healthEntry.ProcessorId, processorName, processorVersion);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex,
                "🔥 DEBUG: FAILED to record health cache entry metrics for processor {ProcessorId}",
                healthEntry.ProcessorId);
        }
    }

    public void RecordProcessorStatus(Guid processorId, HealthStatus status, string processorName, string processorVersion)
    {
        _logger.LogInformationWithCorrelation("🔍 TRACE: Getting system labels from label service...");
        var tags = _labelService.GetSystemLabels();
        _logger.LogInformationWithCorrelation("✅ TRACE: Got {TagCount} labels: {Labels}", tags.Length, string.Join(", ", tags.Select(t => $"{t.Key}={t.Value}")));

        var statusValue = (int)status;

        _logger.LogInformationWithCorrelation("🔍 TRACE: Recording processor_health_status gauge with value {StatusValue}...", statusValue);
        _processorStatusGauge.Record(statusValue, tags);
        _logger.LogInformationWithCorrelation("✅ TRACE: processor_health_status gauge recorded successfully");

        _logger.LogDebugWithCorrelation(
            "🔥 DEBUG: Recorded processor_health_status metric - ProcessorId: {ProcessorId}, Status: {Status} ({StatusValue}), CompositeKey: {CompositeKey}",
            processorId, status, statusValue, _labelService.ProcessorCompositeKey);
    }

    public void RecordProcessorUptime(Guid processorId, TimeSpan uptime, string processorName, string processorVersion)
    {
        _logger.LogInformationWithCorrelation("🔍 TRACE: Getting system labels for uptime metric...");
        var tags = _labelService.GetSystemLabels();
        _logger.LogInformationWithCorrelation("✅ TRACE: Got {TagCount} labels for uptime", tags.Length);

        _logger.LogInformationWithCorrelation("🔍 TRACE: Recording processor_uptime_seconds gauge with value {UptimeSeconds}...", uptime.TotalSeconds);
        _processorUptimeGauge.Record(uptime.TotalSeconds, tags);
        _logger.LogInformationWithCorrelation("✅ TRACE: processor_uptime_seconds gauge recorded successfully");

        _logger.LogDebugWithCorrelation(
            "🔥 DEBUG: Recorded processor_uptime_seconds metric - ProcessorId: {ProcessorId}, Uptime: {Uptime} ({UptimeSeconds}s), CompositeKey: {CompositeKey}",
            processorId, uptime, uptime.TotalSeconds, _labelService.ProcessorCompositeKey);
    }

    public void RecordPerformanceMetrics(Guid processorId, ProcessorPerformanceMetrics performanceMetrics, string processorName, string processorVersion)
    {
        var tags = _labelService.GetSystemLabels();

        // Record current resource usage as gauges
        _cpuUsageGauge.Record(performanceMetrics.CpuUsagePercent, tags);
        _memoryUsageGauge.Record(performanceMetrics.MemoryUsageBytes, tags);

        _logger.LogDebugWithCorrelation(
            "🔥 DEBUG: Recorded performance metrics - ProcessorId: {ProcessorId}, CPU: {CpuUsage}%, Memory: {MemoryUsage} bytes, CompositeKey: {CompositeKey}",
            processorId, performanceMetrics.CpuUsagePercent, performanceMetrics.MemoryUsageBytes, _labelService.ProcessorCompositeKey);
    }

    public void RecordProcessorMetadata(Guid processorId, ProcessorMetadata metadata, string processorName, string processorVersion)
    {
        var tags = _labelService.GetSystemLabels().ToList();
        tags.Add(new KeyValuePair<string, object?>("host_name", metadata.HostName));

        _processorProcessIdGauge.Record(metadata.ProcessId, tags.ToArray());

        // Record processor start event (only once per start time)
        var startTimeKey = $"{processorVersion}_{processorName}_{metadata.StartTime:yyyy-MM-ddTHH:mm:ssZ}";

        lock (_recordedStartTimes)
        {
            if (!_recordedStartTimes.Contains(startTimeKey) )
            {
                _recordedStartTimes.Add(startTimeKey);

                var startTags = _labelService.GetSystemLabels().ToList();
                startTags.Add(new KeyValuePair<string, object?>("start_time", metadata.StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ")));

                _processorStartCounter.Add(1, startTags.ToArray());

                _logger.LogDebugWithCorrelation(
                    "Recorded processor start event for ProcessorId: {ProcessorId}, StartTime: {StartTime}, CompositeKey: {CompositeKey}",
                    processorId, metadata.StartTime, _labelService.ProcessorCompositeKey);
            }
        }
    }

    public void RecordHealthCheckResults(Guid processorId, Dictionary<string, HealthCheckResult> healthChecks, string processorName, string processorVersion)
    {
        foreach (var healthCheck in healthChecks)
        {
            var tags = _labelService.GetHealthLabels(healthCheck.Key, healthCheck.Value.Status.ToString());

            _healthCheckCounter.Add(1, tags);
        }
    }

    public void RecordCacheMetrics(Guid processorId, double averageEntryAge, long activeEntries, string processorName, string processorVersion)
    {
        var tags = _labelService.GetCacheLabels();

        _cacheAverageEntryAgeGauge.Record(averageEntryAge, tags);
        _cacheActiveEntriesGauge.Record(activeEntries, tags);

        _logger.LogDebugWithCorrelation(
            "🔥 DEBUG: Recorded cache metrics - ProcessorId: {ProcessorId}, AverageAge: {AverageAge}s, ActiveEntries: {ActiveEntries}, CompositeKey: {CompositeKey}",
            processorId, averageEntryAge, activeEntries, _labelService.ProcessorCompositeKey);
    }

    /// <summary>
    /// Records exception metrics for processor monitoring.
    /// </summary>
    /// <param name="exceptionType">Type of exception (e.g., ValidationException, ProcessingException)</param>
    /// <param name="severity">Severity level (warning, error, critical)</param>
    /// <param name="isCritical">Whether this exception affects processor operation</param>
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
            exceptionType, severity, isCritical, _labelService.ProcessorCompositeKey);
    }







    public void Dispose()
    {
        _meter?.Dispose();
    }
}
