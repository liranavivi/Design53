using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Correlation;
using Shared.Models;
using Shared.Processor.Models;

namespace Shared.Processor.Services;

/// <summary>
/// Service for collecting processor performance metrics
/// </summary>
public class PerformanceMetricsService : IPerformanceMetricsService
{
    private readonly PerformanceMetricsConfiguration _config;
    private readonly ILogger<PerformanceMetricsService> _logger;
    private readonly Process _currentProcess;
    private readonly object _metricsLock = new();

    // Activity tracking
    private readonly ConcurrentQueue<ActivityRecord> _activityHistory = new();
    private long _totalActivities = 0;
    private long _successfulActivities = 0;
    private long _failedActivities = 0;
    private double _totalExecutionTimeMs = 0;

    // CPU tracking
    private DateTime _lastCpuTime = DateTime.UtcNow;
    private TimeSpan _lastProcessorTime = TimeSpan.Zero;

    public PerformanceMetricsService(
        IOptions<ProcessorHealthMonitorConfiguration> config,
        ILogger<PerformanceMetricsService> logger)
    {
        _config = config.Value.PerformanceMetrics;
        _logger = logger;
        _currentProcess = Process.GetCurrentProcess();
        
        // Initialize CPU tracking
        _lastProcessorTime = _currentProcess.TotalProcessorTime;
        _lastCpuTime = DateTime.UtcNow;
    }

    public async Task<ProcessorPerformanceMetrics> CollectMetricsAsync()
    {
        var metrics = new ProcessorPerformanceMetrics
        {
            CollectedAt = DateTime.UtcNow
        };

        try
        {
            // Collect CPU metrics
            if (_config.CollectCpuMetrics)
            {
                metrics.CpuUsagePercent = await GetCpuUsageAsync();
            }

            // Collect memory metrics
            if (_config.CollectMemoryMetrics)
            {
                metrics.MemoryUsageBytes = GetMemoryUsage();
            }

            // Collect throughput metrics
            if (_config.CollectThroughputMetrics)
            {
                lock (_metricsLock)
                {
                    metrics.TotalActivitiesProcessed = _totalActivities;
                    metrics.SuccessfulActivities = _successfulActivities;
                    metrics.FailedActivities = _failedActivities;
                    metrics.ActivitiesPerMinute = GetCurrentThroughput();
                    metrics.AverageExecutionTimeMs = GetAverageExecutionTime();
                }
            }

            _logger.LogDebugWithCorrelation("Performance metrics collected. CPU: {CpuUsage}%, Memory: {MemoryMB}MB, Throughput: {Throughput}/min",
                metrics.CpuUsagePercent, metrics.MemoryUsageMB, metrics.ActivitiesPerMinute);

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Failed to collect performance metrics");
            return metrics; // Return partial metrics
        }
    }

    public void RecordActivity(bool success, double executionTimeMs)
    {
        var record = new ActivityRecord
        {
            Timestamp = DateTime.UtcNow,
            Success = success,
            ExecutionTimeMs = executionTimeMs
        };

        _activityHistory.Enqueue(record);

        lock (_metricsLock)
        {
            _totalActivities++;
            _totalExecutionTimeMs += executionTimeMs;

            if (success)
            {
                _successfulActivities++;
            }
            else
            {
                _failedActivities++;
            }
        }

        // Clean up old records outside the window
        CleanupOldRecords();
    }

    public double GetCurrentThroughput()
    {
        CleanupOldRecords();
        
        var cutoffTime = DateTime.UtcNow - _config.ThroughputWindow;
        var recentActivities = _activityHistory.Count(r => r.Timestamp >= cutoffTime);
        
        return (recentActivities * 60.0) / _config.ThroughputWindow.TotalMinutes;
    }

    public double GetSuccessRate()
    {
        lock (_metricsLock)
        {
            return _totalActivities > 0 ? (_successfulActivities * 100.0) / _totalActivities : 100.0;
        }
    }

    public double GetAverageExecutionTime()
    {
        lock (_metricsLock)
        {
            return _totalActivities > 0 ? _totalExecutionTimeMs / _totalActivities : 0.0;
        }
    }

    public void Reset()
    {
        lock (_metricsLock)
        {
            _totalActivities = 0;
            _successfulActivities = 0;
            _failedActivities = 0;
            _totalExecutionTimeMs = 0;
        }

        _activityHistory.Clear();
        
        _logger.LogInformationWithCorrelation("Performance metrics reset");
    }

    private Task<double> GetCpuUsageAsync()
    {
        try
        {
            var currentTime = DateTime.UtcNow;
            var currentProcessorTime = _currentProcess.TotalProcessorTime;

            var timeDiff = currentTime - _lastCpuTime;
            var processorTimeDiff = currentProcessorTime - _lastProcessorTime;

            var cpuUsage = (processorTimeDiff.TotalMilliseconds / timeDiff.TotalMilliseconds) * 100.0;

            _lastCpuTime = currentTime;
            _lastProcessorTime = currentProcessorTime;

            return Task.FromResult(Math.Min(100.0, Math.Max(0.0, cpuUsage)));
        }
        catch (Exception ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Failed to calculate CPU usage");
            return Task.FromResult(0.0);
        }
    }

    private long GetMemoryUsage()
    {
        try
        {
            return _currentProcess.WorkingSet64;
        }
        catch (Exception ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Failed to get memory usage");
            return 0;
        }
    }

    private void CleanupOldRecords()
    {
        var cutoffTime = DateTime.UtcNow - _config.ThroughputWindow;
        
        while (_activityHistory.TryPeek(out var record) && record.Timestamp < cutoffTime)
        {
            _activityHistory.TryDequeue(out _);
        }
    }

    private class ActivityRecord
    {
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
        public double ExecutionTimeMs { get; set; }
    }
}
