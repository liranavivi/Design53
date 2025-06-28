using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Correlation;
using Shared.Models;
using Shared.Processor.Models;
using Shared.Services;
using System.Diagnostics;
using System.Text.Json;

namespace Shared.Processor.Services;

/// <summary>
/// Distributed background service that monitors processor health and updates the shared cache.
/// Implements processor-centric health monitoring with last-writer-wins strategy.
/// Multiple pods can run this service for the same processor without coordination.
/// </summary>
public class ProcessorHealthMonitor : BackgroundService, IProcessorHealthMonitor
{
    private readonly IProcessorService _processorService;
    private readonly IPerformanceMetricsService _performanceMetricsService;
    private readonly IProcessorHealthMetricsService _healthMetricsService;
    private readonly ICacheService _cacheService;
    private readonly ProcessorHealthMonitorConfiguration _config;
    private readonly ProcessorConfiguration _processorConfig;
    private readonly ILogger<ProcessorHealthMonitor> _logger;
    private readonly ActivitySource _activitySource;

    // Local concurrency control - prevents redundant health checks within this pod
    private readonly SemaphoreSlim _healthCheckSemaphore = new(1, 1);
    private Timer? _healthCheckTimer;

    // Health check statistics for monitoring the monitoring system
    private long _totalHealthChecks = 0;
    private long _successfulHealthChecks = 0;
    private long _failedHealthChecks = 0;
    private DateTime _lastSuccessfulHealthCheck = DateTime.MinValue;

    // Initialization-aware caching state
    private long _healthChecksSkippedDueToInitialization = 0;
    private long _healthChecksStoredInCache = 0;
    private DateTime _firstProcessorIdAvailableAt = DateTime.MinValue;

    public ProcessorHealthMonitor(
        IProcessorService processorService,
        IPerformanceMetricsService performanceMetricsService,
        IProcessorHealthMetricsService healthMetricsService,
        ICacheService cacheService,
        IOptions<ProcessorHealthMonitorConfiguration> config,
        IOptions<ProcessorConfiguration> processorConfig,
        ILogger<ProcessorHealthMonitor> logger)
    {
        _processorService = processorService;
        _performanceMetricsService = performanceMetricsService;
        _healthMetricsService = healthMetricsService;
        _cacheService = cacheService;
        _config = config.Value;
        _processorConfig = processorConfig.Value;
        _logger = logger;
        _activitySource = new ActivitySource("BaseProcessorApplication.HealthMonitor");
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformationWithCorrelation("Processor health monitoring is disabled");
            return;
        }

        _logger.LogInformationWithCorrelation("Starting processor health monitor. Interval: {Interval}s, TTL: {TTL}s",
            _config.HealthCheckInterval.TotalSeconds, _config.HealthCacheTtl.TotalSeconds);

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformationWithCorrelation("Stopping processor health monitor");

        _healthCheckTimer?.Dispose();
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            return;
        }

        _logger.LogInformationWithCorrelation(
            "Starting initialization-aware health monitoring. " +
            "Service starts immediately, health data cached only when ProcessorId is available. " +
            "Interval: {Interval}, PodId: {PodId}",
            _config.HealthCheckInterval, _config.PodId);

        // Perform initial health check immediately (initialization-aware)
        await PerformHealthCheckAsync();

        // Set up periodic health checks - runs on fixed schedule regardless of initialization status
        _healthCheckTimer = new Timer(
            async _ => await PerformHealthCheckAsync(),
            null,
            _config.HealthCheckInterval, // First check after interval
            _config.HealthCheckInterval); // Subsequent checks at regular intervals

        // Keep the service running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    public async Task PerformHealthCheckAsync()
    {
        // Local concurrency control - only one health check per pod at a time
        if (!await _healthCheckSemaphore.WaitAsync(TimeSpan.FromSeconds(5)))
        {
            _logger.LogWarningWithCorrelation("Health check already in progress in this pod, skipping cycle. PodId: {PodId}", _config.PodId);
            return;
        }

        var healthCheckId = Guid.NewGuid();
        Interlocked.Increment(ref _totalHealthChecks);

        // Generate and set correlation ID for this health check operation
        var correlationId = Guid.NewGuid();
        CorrelationIdContext.SetCorrelationIdStatic(correlationId);

        try
        {
            using var activity = _activitySource.StartActivity("PerformDistributedHealthCheck");
            activity?.SetTag("health_check.id", healthCheckId.ToString())
                    ?.SetTag("pod.id", _config.PodId)
                    ?.SetTag("correlation.id", correlationId.ToString());

            var stopwatch = Stopwatch.StartNew();

            _logger.LogDebugWithCorrelation("Starting health check {HealthCheckId} from pod {PodId}", healthCheckId, _config.PodId);

            // Get processor health status
            var healthStatus = await _processorService.GetHealthStatusAsync();
            var processorId = healthStatus.ProcessorId;

            // Initialization-aware caching: Check if ProcessorId is available
            var isProcessorInitialized = processorId != Guid.Empty;

            activity?.SetTag("processor.id", processorId.ToString())
                    ?.SetTag("processor.status", healthStatus.Status.ToString())
                    ?.SetTag("processor.initialized", isProcessorInitialized);

            // Collect performance metrics if enabled
            ProcessorPerformanceMetrics? performanceMetrics = null;
            if (_config.IncludePerformanceMetrics)
            {
                performanceMetrics = await _performanceMetricsService.CollectMetricsAsync();

                // Record performance metrics for export to OpenTelemetry
                if (performanceMetrics != null && isProcessorInitialized)
                {
                    _healthMetricsService.RecordPerformanceMetrics(processorId, performanceMetrics, _processorConfig.Name, _processorConfig.Version);

                    _logger.LogDebugWithCorrelation(
                        "🔥 DEBUG: Recorded performance metrics - CPU: {CpuUsage}%, Memory: {MemoryUsage} bytes",
                        performanceMetrics.CpuUsagePercent, performanceMetrics.MemoryUsageBytes);
                }
            }

            // Create health cache entry with processor-centric design
            var healthEntry = new ProcessorHealthCacheEntry
            {
                ProcessorId = processorId,
                Status = healthStatus.Status,
                LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                HealthCheckInterval = (int)_config.HealthCheckInterval.TotalSeconds,
                ExpiresAt = DateTime.UtcNow.Add(_config.HealthCacheTtl),
                ReportingPodId = _config.PodId, // Track which pod reported this
                CorrelationId = correlationId, // Use the same correlation ID as logging context
                HealthCheckId = healthCheckId, // For tracing specific health check executions
                Message = healthStatus.Message,
                Uptime = healthStatus.Uptime,
                Metadata = healthStatus.Metadata,
                PerformanceMetrics = performanceMetrics ?? new ProcessorPerformanceMetrics(),
                HealthChecks = _config.IncludeDetailedHealthChecks ? healthStatus.HealthChecks : new()
            };

            // Always record health cache entry metrics for analysis, regardless of ProcessorId
            _healthMetricsService.RecordHealthCacheEntryMetrics(healthEntry);

            // Record real cache statistics if processor is initialized
            if (isProcessorInitialized)
            {
                try
                {
                    var (entryCount, averageAge) = await _cacheService.GetCacheStatisticsAsync(_config.HealthCacheMapName);
                    _healthMetricsService.RecordCacheMetrics(processorId, averageAge, entryCount, _processorConfig.Name, _processorConfig.Version);

                    _logger.LogDebugWithCorrelation(
                        "🔥 DEBUG: Recorded real cache metrics - EntryCount: {EntryCount}, AverageAge: {AverageAge}s",
                        entryCount, averageAge);
                }
                catch (Exception ex)
                {
                    _logger.LogWarningWithCorrelation(ex, "Failed to collect cache statistics for metrics");
                }
            }

            // Initialization-aware caching: Only store in cache when ProcessorId is available
            if (isProcessorInitialized)
            {
                // Track first time ProcessorId becomes available
                if (_firstProcessorIdAvailableAt == DateTime.MinValue)
                {
                    _firstProcessorIdAvailableAt = DateTime.UtcNow;
                    _logger.LogInformationWithCorrelation(
                        "ProcessorId now available, beginning cache storage. ProcessorId: {ProcessorId}, PodId: {PodId}, " +
                        "HealthChecksSkipped: {SkippedCount}",
                        processorId, _config.PodId, _healthChecksSkippedDueToInitialization);
                }

                // Store in distributed cache using last-writer-wins strategy
                var cacheKey = processorId.ToString();
                var cacheValue = JsonSerializer.Serialize(healthEntry);

                await StoreHealthEntryWithRetry(cacheKey, cacheValue);
                Interlocked.Increment(ref _healthChecksStoredInCache);
            }
            else
            {
                // Gracefully skip cache storage if ProcessorId is not yet available
                Interlocked.Increment(ref _healthChecksSkippedDueToInitialization);

                _logger.LogDebugWithCorrelation(
                    "Health check {HealthCheckId} completed but ProcessorId not yet available, skipping cache storage. " +
                    "Metrics recorded. PodId: {PodId}, Status: {Status}, SkippedCount: {SkippedCount}",
                    healthCheckId, _config.PodId, healthStatus.Status, _healthChecksSkippedDueToInitialization);
            }

            stopwatch.Stop();
            Interlocked.Increment(ref _successfulHealthChecks);
            _lastSuccessfulHealthCheck = DateTime.UtcNow;

            if (_config.LogHealthChecks)
            {
                LogHealthCheckResult(healthEntry, stopwatch.Elapsed, healthCheckId, isProcessorInitialized);
            }

            activity?.SetTag("health_check.duration_ms", stopwatch.ElapsedMilliseconds)
                    ?.SetTag("health_check.success", true)
                    ?.SetTag("cache_stored", isProcessorInitialized);

            _logger.LogDebugWithCorrelation(
                "Completed health check {HealthCheckId} for processor {ProcessorId} from pod {PodId} in {Duration}ms. " +
                "Initialized: {Initialized}, CacheStored: {CacheStored}, CorrelationId: {CorrelationId}",
                healthCheckId, processorId, _config.PodId, stopwatch.ElapsedMilliseconds,
                isProcessorInitialized, isProcessorInitialized, healthEntry.CorrelationId);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedHealthChecks);

            _logger.LogErrorWithCorrelation(ex, "Failed to perform health check {HealthCheckId} from pod {PodId}", healthCheckId, _config.PodId);

            if (!_config.ContinueOnCacheFailure)
            {
                throw;
            }
        }
        finally
        {
            _healthCheckSemaphore.Release();
        }
    }

    public async Task<ProcessorHealthCacheEntry?> GetHealthStatusFromCacheAsync(Guid processorId)
    {
        try
        {
            var cacheKey = processorId.ToString();
            var cacheValue = await _cacheService.GetAsync(_config.HealthCacheMapName, cacheKey);

            if (string.IsNullOrEmpty(cacheValue))
            {
                _logger.LogDebugWithCorrelation("No health status found in cache for ProcessorId: {ProcessorId}", processorId);
                return null;
            }

            var healthEntry = JsonSerializer.Deserialize<ProcessorHealthCacheEntry>(cacheValue);

            // Check if the entry has expired
            if (healthEntry != null && healthEntry.IsExpired)
            {
                _logger.LogWarningWithCorrelation("Health status for ProcessorId: {ProcessorId} has expired. LastUpdated: {LastUpdated}, ExpiresAt: {ExpiresAt}",
                    processorId, healthEntry.LastUpdated, healthEntry.ExpiresAt);
                return null;
            }

            return healthEntry;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Failed to get health status from cache for ProcessorId: {ProcessorId}", processorId);
            return null;
        }
    }

    public Task<Dictionary<Guid, ProcessorHealthCacheEntry>> GetAllHealthStatusFromCacheAsync()
    {
        var result = new Dictionary<Guid, ProcessorHealthCacheEntry>();

        try
        {
            // Note: This is a simplified implementation
            // In a real scenario, you might want to implement a method to get all keys from a map
            _logger.LogWarningWithCorrelation("GetAllHealthStatusFromCacheAsync is not fully implemented - requires Hazelcast map iteration");
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Failed to get all health status from cache");
            return Task.FromResult(result);
        }
    }

    private async Task StoreHealthEntryWithRetry(string cacheKey, string cacheValue)
    {
        var retryCount = 0;
        var baseDelay = _config.RetryDelay;

        while (retryCount <= _config.MaxRetries)
        {
            try
            {
                // Use last-writer-wins strategy - no distributed locking needed
                await _cacheService.SetAsync(_config.HealthCacheMapName, cacheKey, cacheValue, _config.HealthCacheTtl);

                _logger.LogDebugWithCorrelation("Successfully stored health entry for key {CacheKey} from pod {PodId}", cacheKey, _config.PodId);
                return; // Success
            }
            catch (Exception ex)
            {
                retryCount++;

                if (retryCount > _config.MaxRetries)
                {
                    _logger.LogErrorWithCorrelation(ex, "Failed to store health entry for key {CacheKey} after {MaxRetries} retries from pod {PodId}",
                        cacheKey, _config.MaxRetries, _config.PodId);
                    throw;
                }

                // Calculate delay with optional exponential backoff
                var delay = _config.UseExponentialBackoff
                    ? TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, retryCount - 1))
                    : baseDelay;

                _logger.LogWarningWithCorrelation(ex, "Failed to store health entry for key {CacheKey}, retry {RetryCount}/{MaxRetries} in {Delay}ms from pod {PodId}",
                    cacheKey, retryCount, _config.MaxRetries, delay.TotalMilliseconds, _config.PodId);

                await Task.Delay(delay);
            }
        }
    }

    private void LogHealthCheckResult(ProcessorHealthCacheEntry healthEntry, TimeSpan duration, Guid healthCheckId, bool isProcessorInitialized)
    {
        var logLevel = _config.LogLevel.ToLowerInvariant() switch
        {
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "debug" => LogLevel.Debug,
            _ => LogLevel.Information
        };

        var successRate = _totalHealthChecks > 0 ? (_successfulHealthChecks * 100.0) / _totalHealthChecks : 100.0;
        var cacheStorageRate = _totalHealthChecks > 0 ? (_healthChecksStoredInCache * 100.0) / _totalHealthChecks : 0.0;

        _logger.Log(logLevel,
            "Health check {HealthCheckId} completed. ProcessorId: {ProcessorId}, Status: {Status}, Duration: {Duration}ms, " +
            "CPU: {CpuUsage}%, Memory: {MemoryMB}MB, PodId: {PodId}, Initialized: {Initialized}, CacheStored: {CacheStored}, " +
            "CorrelationId: {CorrelationId}, SuccessRate: {SuccessRate:F1}%, CacheRate: {CacheRate:F1}%, " +
            "Total: {TotalChecks}, Successful: {SuccessfulChecks}, Failed: {FailedChecks}, Skipped: {SkippedChecks}, Cached: {CachedChecks}",
            healthCheckId,
            healthEntry.ProcessorId,
            healthEntry.Status,
            duration.TotalMilliseconds,
            healthEntry.PerformanceMetrics.CpuUsagePercent,
            healthEntry.PerformanceMetrics.MemoryUsageMB,
            _config.PodId,
            isProcessorInitialized,
            isProcessorInitialized,
            healthEntry.CorrelationId,
            successRate,
            cacheStorageRate,
            _totalHealthChecks,
            _successfulHealthChecks,
            _failedHealthChecks,
            _healthChecksSkippedDueToInitialization,
            _healthChecksStoredInCache);
    }

    /// <summary>
    /// Gets health monitoring statistics for this pod.
    /// Useful for monitoring the health monitoring system itself.
    /// </summary>
    public HealthMonitoringStatistics GetMonitoringStatistics()
    {
        return new HealthMonitoringStatistics
        {
            PodId = _config.PodId,
            TotalHealthChecks = _totalHealthChecks,
            SuccessfulHealthChecks = _successfulHealthChecks,
            FailedHealthChecks = _failedHealthChecks,
            HealthChecksSkippedDueToInitialization = _healthChecksSkippedDueToInitialization,
            HealthChecksStoredInCache = _healthChecksStoredInCache,
            SuccessRate = _totalHealthChecks > 0 ? (_successfulHealthChecks * 100.0) / _totalHealthChecks : 100.0,
            CacheStorageRate = _totalHealthChecks > 0 ? (_healthChecksStoredInCache * 100.0) / _totalHealthChecks : 0.0,
            LastSuccessfulHealthCheck = _lastSuccessfulHealthCheck,
            FirstProcessorIdAvailableAt = _firstProcessorIdAvailableAt,
            IsHealthy = _lastSuccessfulHealthCheck > DateTime.UtcNow.AddMinutes(-5), // Healthy if successful check in last 5 minutes
            IsProcessorInitialized = _firstProcessorIdAvailableAt != DateTime.MinValue
        };
    }

    public override void Dispose()
    {
        _healthCheckTimer?.Dispose();
        _healthCheckSemaphore?.Dispose();
        _activitySource?.Dispose();
        base.Dispose();
    }
}
