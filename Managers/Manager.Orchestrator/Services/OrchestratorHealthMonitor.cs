using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shared.Correlation;
using Shared.Extensions;
using Shared.Models;
using Shared.Services;
using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;
using ManagerConfiguration = Shared.Models.ManagerConfiguration;

namespace Manager.Orchestrator.Services;

/// <summary>
/// Background service that monitors orchestrator health following the processor health monitoring pattern.
/// Performs periodic health checks and records health status and cache metrics.
/// </summary>
public class OrchestratorHealthMonitor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrchestratorHealthMonitor> _logger;
    private readonly OrchestratorHealthMonitorConfiguration _config;
    private readonly ManagerConfiguration _managerConfig;
    private readonly DateTime _startTime;

    private Timer? _healthCheckTimer;
    private readonly SemaphoreSlim _healthCheckSemaphore = new(1, 1);
    private long _totalHealthChecks = 0;
    private long _successfulHealthChecks = 0;
    private long _failedHealthChecks = 0;
    private DateTime _lastSuccessfulHealthCheck = DateTime.MinValue;

    public OrchestratorHealthMonitor(
        IServiceProvider serviceProvider,
        IOptions<OrchestratorHealthMonitorConfiguration> config,
        IOptions<ManagerConfiguration> managerConfig,
        ILogger<OrchestratorHealthMonitor> logger)
    {
        _serviceProvider = serviceProvider;
        _config = config.Value;
        _managerConfig = managerConfig.Value;
        _logger = logger;
        _startTime = DateTime.UtcNow;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformationWithCorrelation("Orchestrator health monitoring is disabled");
            return;
        }

        _logger.LogInformationWithCorrelation(
            "Starting orchestrator health monitoring. Interval: {Interval}, Manager: {ManagerName} v{ManagerVersion}",
            _config.HealthCheckInterval, _managerConfig.Name, _managerConfig.Version);

        // Perform initial health check immediately
        await PerformHealthCheckAsync();

        // Set up periodic health checks
        _healthCheckTimer = new Timer(
            async _ => await PerformHealthCheckAsync(),
            null,
            _config.HealthCheckInterval,
            _config.HealthCheckInterval);

        // Keep the service running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    public async Task PerformHealthCheckAsync()
    {
        // Local concurrency control - only one health check at a time
        if (!await _healthCheckSemaphore.WaitAsync(TimeSpan.FromSeconds(5)))
        {
            _logger.LogWarningWithCorrelation("Health check already in progress, skipping cycle");
            return;
        }

        var healthCheckId = Guid.NewGuid();
        Interlocked.Increment(ref _totalHealthChecks);

        try
        {
            var stopwatch = Stopwatch.StartNew();

            _logger.LogDebugWithCorrelation(
                "🔍 Starting orchestrator health check {HealthCheckId}. Manager: {ManagerName} v{ManagerVersion}",
                healthCheckId, _managerConfig.Name, _managerConfig.Version);

            // Perform .NET health checks and record metrics using scoped services
            using var scope = _serviceProvider.CreateScope();
            var healthCheckService = scope.ServiceProvider.GetRequiredService<HealthCheckService>();
            var metricsService = scope.ServiceProvider.GetRequiredService<IOrchestratorHealthMetricsService>();

            var healthReport = await healthCheckService.CheckHealthAsync();
            stopwatch.Stop();

            _logger.LogDebugWithCorrelation(
                "🔥 DEBUG: Health checks completed in {Duration}ms. Overall: {OverallStatus}, Individual checks: {CheckCount}",
                stopwatch.ElapsedMilliseconds, healthReport.Status, healthReport.Entries.Count);

            // Record overall health status (0=Healthy, 1=Degraded, 2=Unhealthy)
            var overallStatus = healthReport.Status switch
            {
                HealthStatus.Healthy => 0,
                HealthStatus.Degraded => 1,
                HealthStatus.Unhealthy => 2,
                _ => 2
            };

            // Record overall health status and uptime
            metricsService.RecordOrchestratorStatus(overallStatus);
            metricsService.RecordOrchestratorUptime();

            // Convert health report entries to processor-style dictionary
            var healthCheckResults = healthReport.Entries.ToDictionary(
                entry => entry.Key,
                entry => new Shared.Models.HealthCheckResult
                {
                    Status = entry.Value.Status switch
                    {
                        HealthStatus.Healthy => Shared.Models.HealthStatus.Healthy,
                        HealthStatus.Degraded => Shared.Models.HealthStatus.Degraded,
                        HealthStatus.Unhealthy => Shared.Models.HealthStatus.Unhealthy,
                        _ => Shared.Models.HealthStatus.Unhealthy
                    },
                    Description = entry.Value.Description ?? string.Empty,
                    Duration = entry.Value.Duration,
                    Exception = entry.Value.Exception?.ToString(),
                    Data = entry.Value.Data?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>()
                });

            // Record health check results following processor pattern
            metricsService.RecordHealthCheckResults(healthCheckResults);

            // Keep individual logging for detailed monitoring
            foreach (var entry in healthReport.Entries)
            {
                _logger.LogDebugWithCorrelation(
                    "🔥 DEBUG: Recorded health check metric - Name: {HealthCheckName}, Status: {Status}, Duration: {Duration}ms",
                    entry.Key, entry.Value.Status, entry.Value.Duration.TotalMilliseconds);
            }

            // Record cache statistics
            await RecordCacheStatisticsAsync(scope.ServiceProvider);

            // Record performance metrics
            await RecordPerformanceMetricsAsync(scope.ServiceProvider);

            // Record metadata metrics
            RecordMetadataMetrics(scope.ServiceProvider);

            Interlocked.Increment(ref _successfulHealthChecks);
            _lastSuccessfulHealthCheck = DateTime.UtcNow;

            if (_config.LogHealthChecks)
            {
                LogHealthCheckResult(healthCheckId, healthReport, stopwatch.Elapsed);
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedHealthChecks);

            _logger.LogErrorWithCorrelation(ex, "Failed to perform orchestrator health check {HealthCheckId}", healthCheckId);

            // Record unhealthy status on failure using scoped service
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var metricsService = scope.ServiceProvider.GetRequiredService<IOrchestratorHealthMetricsService>();

                // Record health check exception as critical
                metricsService.RecordException(ex.GetType().Name, "error", isCritical: true);
                metricsService.RecordOrchestratorStatus(2); // 2 = Unhealthy
            }
            catch (Exception metricsEx)
            {
                _logger.LogWarningWithCorrelation(metricsEx, "Failed to record unhealthy status metric");
            }

            if (!_config.ContinueOnFailure)
            {
                throw;
            }
        }
        finally
        {
            _healthCheckSemaphore.Release();
        }
    }

    private async Task RecordCacheStatisticsAsync(IServiceProvider serviceProvider)
    {
        try
        {
            var hazelcastCacheService = serviceProvider.GetRequiredService<ICacheService>();
            var metricsService = serviceProvider.GetRequiredService<IOrchestratorHealthMetricsService>();

            // Get cache statistics from Hazelcast
            var (entryCount, averageAge) = await hazelcastCacheService.GetCacheStatisticsAsync(_config.CacheMapName);

            // Record comprehensive cache metrics (new method following processor pattern)
            metricsService.RecordCacheMetrics(averageAge, entryCount);

            // Keep existing method for backward compatibility
            metricsService.UpdateActiveCacheEntries(entryCount);

            _logger.LogDebugWithCorrelation(
                "🔥 DEBUG: Recorded orchestrator cache metrics - EntryCount: {EntryCount}, AverageAge: {AverageAge}s, " +
                "Manager: {ManagerName} v{ManagerVersion}, CacheMap: {CacheMapName}",
                entryCount, averageAge, _managerConfig.Name, _managerConfig.Version, _config.CacheMapName);
        }
        catch (Exception ex)
        {
            // Record cache statistics collection exception as non-critical
            try
            {
                var metricsService = serviceProvider.GetRequiredService<IOrchestratorHealthMetricsService>();
                metricsService.RecordException(ex.GetType().Name, "warning", isCritical: false);
            }
            catch
            {
                // Ignore metrics recording errors during exception handling
            }

            _logger.LogWarningWithCorrelation(ex,
                "Failed to collect orchestrator cache statistics for metrics. Manager: {ManagerName} v{ManagerVersion}",
                _managerConfig.Name, _managerConfig.Version);
        }
    }

    private async Task RecordPerformanceMetricsAsync(IServiceProvider serviceProvider)
    {
        try
        {
            var metricsService = serviceProvider.GetRequiredService<IOrchestratorHealthMetricsService>();
            var currentProcess = Process.GetCurrentProcess();

            // Get current memory usage
            var memoryUsageBytes = currentProcess.WorkingSet64;

            // Get CPU usage (simplified approach - could be enhanced with more sophisticated CPU monitoring)
            var cpuUsagePercent = await GetCpuUsageAsync(currentProcess);

            // Record performance metrics
            metricsService.RecordPerformanceMetrics(cpuUsagePercent, memoryUsageBytes);

            _logger.LogDebugWithCorrelation(
                "🔥 DEBUG: Recorded orchestrator performance metrics - CPU: {CpuUsage}%, Memory: {MemoryUsage} bytes, " +
                "Manager: {ManagerName} v{ManagerVersion}",
                cpuUsagePercent, memoryUsageBytes, _managerConfig.Name, _managerConfig.Version);
        }
        catch (Exception ex)
        {
            // Record performance metrics collection exception as non-critical
            try
            {
                var metricsService = serviceProvider.GetRequiredService<IOrchestratorHealthMetricsService>();
                metricsService.RecordException(ex.GetType().Name, "warning", isCritical: false);
            }
            catch
            {
                // Ignore metrics recording errors during exception handling
            }

            _logger.LogWarningWithCorrelation(ex,
                "Failed to collect orchestrator performance metrics. Manager: {ManagerName} v{ManagerVersion}",
                _managerConfig.Name, _managerConfig.Version);
        }
    }

    private void RecordMetadataMetrics(IServiceProvider serviceProvider)
    {
        try
        {
            var metricsService = serviceProvider.GetRequiredService<IOrchestratorHealthMetricsService>();
            var currentProcess = Process.GetCurrentProcess();

            // Record orchestrator metadata metrics following processor pattern
            metricsService.RecordOrchestratorMetadata(currentProcess.Id, _startTime);

            _logger.LogDebugWithCorrelation(
                "🔥 DEBUG: Recorded orchestrator metadata metrics - ProcessId: {ProcessId}, StartTime: {StartTime}, " +
                "Manager: {ManagerName} v{ManagerVersion}",
                currentProcess.Id, _startTime, _managerConfig.Name, _managerConfig.Version);
        }
        catch (Exception ex)
        {
            // Record metadata metrics collection exception as non-critical
            try
            {
                var metricsService = serviceProvider.GetRequiredService<IOrchestratorHealthMetricsService>();
                metricsService.RecordException(ex.GetType().Name, "warning", isCritical: false);
            }
            catch
            {
                // Ignore metrics recording errors during exception handling
            }

            _logger.LogWarningWithCorrelation(ex,
                "Failed to record orchestrator metadata metrics. Manager: {ManagerName} v{ManagerVersion}",
                _managerConfig.Name, _managerConfig.Version);
        }
    }

    private async Task<double> GetCpuUsageAsync(Process process)
    {
        try
        {
            // Simple CPU usage calculation - could be enhanced with more sophisticated monitoring
            var startTime = DateTime.UtcNow;
            var startCpuUsage = process.TotalProcessorTime;

            // Wait a short period to measure CPU usage
            await Task.Delay(100);

            var endTime = DateTime.UtcNow;
            var endCpuUsage = process.TotalProcessorTime;

            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

            return Math.Min(cpuUsageTotal * 100, 100.0); // Cap at 100%
        }
        catch (Exception ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Failed to calculate CPU usage, returning 0");
            return 0.0;
        }
    }

    private void LogHealthCheckResult(Guid healthCheckId, HealthReport healthReport, TimeSpan duration)
    {
        var logLevel = _config.LogLevel.ToLowerInvariant() switch
        {
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "debug" => LogLevel.Debug,
            _ => LogLevel.Information
        };

        var successRate = _totalHealthChecks > 0 ? (_successfulHealthChecks * 100.0) / _totalHealthChecks : 100.0;
        var failureRate = _totalHealthChecks > 0 ? (_failedHealthChecks * 100.0) / _totalHealthChecks : 0.0;

        // Get individual health check details
        var healthCheckDetails = string.Join(", ", healthReport.Entries.Select(e =>
            $"{e.Key}:{e.Value.Status}({e.Value.Duration.TotalMilliseconds:F0}ms)"));

        _logger.Log(logLevel,
            "🏥 Orchestrator Health Check {HealthCheckId} completed. " +
            "Overall: {OverallStatus}, Duration: {Duration}ms, Manager: {ManagerName} v{ManagerVersion}, " +
            "Checks: [{HealthCheckDetails}], " +
            "Stats: Success={SuccessRate:F1}% ({SuccessfulChecks}/{TotalChecks}), " +
            "Failed={FailureRate:F1}% ({FailedChecks}), LastSuccess: {LastSuccessTime}",
            healthCheckId,
            healthReport.Status,
            duration.TotalMilliseconds,
            _managerConfig.Name,
            _managerConfig.Version,
            healthCheckDetails,
            successRate,
            _successfulHealthChecks,
            _totalHealthChecks,
            failureRate,
            _failedHealthChecks,
            _lastSuccessfulHealthCheck == DateTime.MinValue ? "Never" : _lastSuccessfulHealthCheck.ToString("HH:mm:ss"));
    }

    public override void Dispose()
    {
        _healthCheckTimer?.Dispose();
        _healthCheckSemaphore?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Configuration for orchestrator health monitoring following processor pattern
/// </summary>
public class OrchestratorHealthMonitorConfiguration
{
    /// <summary>
    /// Whether health monitoring is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Interval between health checks
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Cache map name for orchestration data
    /// </summary>
    public string CacheMapName { get; set; } = "orchestration-cache";

    /// <summary>
    /// Whether to log health check results
    /// </summary>
    public bool LogHealthChecks { get; set; } = true;

    /// <summary>
    /// Log level for health check logging (Information, Warning, Error, Debug)
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Whether to continue on health check failures
    /// </summary>
    public bool ContinueOnFailure { get; set; } = true;

    /// <summary>
    /// Maximum number of retries for cache operations
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Whether to use exponential backoff for retries
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;
}
