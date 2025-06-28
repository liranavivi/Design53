namespace Shared.Processor.Models;

/// <summary>
/// Configuration for the distributed processor health monitoring system.
/// Designed for processor-centric health monitoring with last-writer-wins strategy.
/// </summary>
public class ProcessorHealthMonitorConfiguration
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "ProcessorHealthMonitor";

    /// <summary>
    /// Whether health monitoring is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Interval between health checks (default: 30 seconds).
    /// Longer intervals reduce cache load in distributed environments.
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Time-to-live for health cache entries (default: 2 minutes).
    /// Should be longer than HealthCheckInterval to prevent gaps.
    /// </summary>
    public TimeSpan HealthCacheTtl { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Name of the Hazelcast map for processor health cache
    /// </summary>
    public string HealthCacheMapName { get; set; } = "processor-health";

    /// <summary>
    /// Whether to include performance metrics in health reports
    /// </summary>
    public bool IncludePerformanceMetrics { get; set; } = true;

    /// <summary>
    /// Whether to include detailed health check results.
    /// Detailed checks provide more information but increase cache payload size.
    /// </summary>
    public bool IncludeDetailedHealthChecks { get; set; } = true;

    /// <summary>
    /// Maximum number of retries for health cache operations.
    /// Higher values improve reliability but may increase latency.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts.
    /// Exponential backoff is applied automatically.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Whether to log health check results.
    /// Useful for debugging but can generate significant log volume.
    /// </summary>
    public bool LogHealthChecks { get; set; } = true;

    /// <summary>
    /// Log level for health check logging (Information, Warning, Error).
    /// Controls verbosity of health monitoring logs.
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Whether to continue health monitoring if cache operations fail.
    /// When true, processor continues running even if health reporting fails.
    /// </summary>
    public bool ContinueOnCacheFailure { get; set; } = true;

    /// <summary>
    /// Unique identifier for this pod instance.
    /// Used for debugging and tracing health reports.
    /// </summary>
    public string PodId { get; set; } = Environment.MachineName + "-" + Environment.ProcessId;

    /// <summary>
    /// Whether to use exponential backoff for retry delays.
    /// Helps reduce cache load during outages.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Performance metrics collection configuration
    /// </summary>
    public PerformanceMetricsConfiguration PerformanceMetrics { get; set; } = new();
}

/// <summary>
/// Configuration for performance metrics collection
/// </summary>
public class PerformanceMetricsConfiguration
{
    /// <summary>
    /// Whether to collect CPU usage metrics
    /// </summary>
    public bool CollectCpuMetrics { get; set; } = true;

    /// <summary>
    /// Whether to collect memory usage metrics
    /// </summary>
    public bool CollectMemoryMetrics { get; set; } = true;

    /// <summary>
    /// Whether to collect activity throughput metrics
    /// </summary>
    public bool CollectThroughputMetrics { get; set; } = true;

    /// <summary>
    /// Window size for calculating throughput metrics (default: 5 minutes)
    /// </summary>
    public TimeSpan ThroughputWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to collect garbage collection metrics
    /// </summary>
    public bool CollectGcMetrics { get; set; } = true;

    /// <summary>
    /// Whether to collect thread pool metrics
    /// </summary>
    public bool CollectThreadPoolMetrics { get; set; } = false;
}

/// <summary>
/// Statistics about the health monitoring system itself.
/// Used to monitor the health of the health monitoring with initialization-aware metrics.
/// </summary>
public class HealthMonitoringStatistics
{
    /// <summary>
    /// Identifier of the pod reporting these statistics
    /// </summary>
    public string PodId { get; set; } = string.Empty;

    /// <summary>
    /// Total number of health checks attempted by this pod
    /// </summary>
    public long TotalHealthChecks { get; set; }

    /// <summary>
    /// Number of successful health checks by this pod
    /// </summary>
    public long SuccessfulHealthChecks { get; set; }

    /// <summary>
    /// Number of failed health checks by this pod
    /// </summary>
    public long FailedHealthChecks { get; set; }

    /// <summary>
    /// Number of health checks skipped due to ProcessorId not being available
    /// </summary>
    public long HealthChecksSkippedDueToInitialization { get; set; }

    /// <summary>
    /// Number of health checks that were successfully stored in cache
    /// </summary>
    public long HealthChecksStoredInCache { get; set; }

    /// <summary>
    /// Success rate percentage (0-100)
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Cache storage rate percentage (0-100) - how many checks resulted in cache storage
    /// </summary>
    public double CacheStorageRate { get; set; }

    /// <summary>
    /// Timestamp of the last successful health check
    /// </summary>
    public DateTime LastSuccessfulHealthCheck { get; set; }

    /// <summary>
    /// Timestamp when ProcessorId first became available
    /// </summary>
    public DateTime FirstProcessorIdAvailableAt { get; set; }

    /// <summary>
    /// Whether the health monitoring system itself is healthy
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Whether the processor has been initialized (ProcessorId is available)
    /// </summary>
    public bool IsProcessorInitialized { get; set; }
}
