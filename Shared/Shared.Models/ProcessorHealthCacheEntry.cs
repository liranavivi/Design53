using System.Text.Json.Serialization;

namespace Shared.Models;

/// <summary>
/// Represents a processor health entry stored in the distributed cache.
/// Designed for processor-centric health monitoring with last-writer-wins strategy.
/// Multiple pods can report health for the same processor without coordination.
/// </summary>
public class ProcessorHealthCacheEntry
{
    /// <summary>
    /// Correlation identifier for tracing health check events across systems.
    /// Used to correlate health data in cache with logs in Elasticsearch.
    /// </summary>
    [JsonPropertyName("correlationId")]
    public Guid CorrelationId { get; set; } 

    /// <summary>
    /// Unique identifier for this specific health check execution.
    /// Used to trace individual health check runs in logs and monitoring systems.
    /// </summary>
    [JsonPropertyName("healthCheckId")]
    public Guid HealthCheckId { get; set; } 
    
    /// <summary>
    /// Unique identifier of the processor (not the pod).
    /// Multiple pods running the same processor share this ID.
    /// </summary>
    [JsonPropertyName("processorId")]
    public Guid ProcessorId { get; set; }

    /// <summary>
    /// Current health status of the processor.
    /// Represents the overall processor health, not individual pod health.
    /// </summary>
    [JsonPropertyName("status")]
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Timestamp when this health entry was last updated as Unix time.
    /// Used for last-writer-wins conflict resolution.
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public long LastUpdated { get; set; }

    /// <summary>
    /// Health check interval in seconds from ProcessorHealthMonitor configuration.
    /// Indicates how frequently health checks are performed for this processor.
    /// </summary>
    [JsonPropertyName("healthCheckInterval")]
    public int HealthCheckInterval { get; set; }

    /// <summary>
    /// Timestamp when this entry expires and should be considered stale.
    /// Prevents stale health data from being used by orchestrators.
    /// </summary>
    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Identifier of the pod that reported this health status.
    /// Used for debugging and tracing, not for health decisions.
    /// </summary>
    [JsonPropertyName("reportingPodId")]
    public string ReportingPodId { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed health check results
    /// </summary>
    [JsonPropertyName("healthChecks")]
    public Dictionary<string, HealthCheckResult> HealthChecks { get; set; } = new();

    /// <summary>
    /// Processor metadata information.
    /// Contains processor-level information, not pod-specific details.
    /// </summary>
    [JsonPropertyName("metadata")]
    public ProcessorMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Performance metrics for the processor.
    /// Aggregated metrics that represent processor performance.
    /// </summary>
    [JsonPropertyName("performanceMetrics")]
    public ProcessorPerformanceMetrics PerformanceMetrics { get; set; } = new();

    /// <summary>
    /// Overall health message describing the processor status.
    /// Human-readable description of health status and any issues.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Processor uptime since last restart.
    /// Represents how long the processor has been running.
    /// </summary>
    [JsonPropertyName("uptime")]
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Indicates if this health entry is still valid based on TTL.
    /// </summary>
    [JsonIgnore]
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Time remaining until this health entry expires.
    /// </summary>
    [JsonIgnore]
    public TimeSpan TimeToExpiration => ExpiresAt > DateTime.UtcNow ? ExpiresAt - DateTime.UtcNow : TimeSpan.Zero;
}

/// <summary>
/// Processor metadata information
/// </summary>
public class ProcessorMetadata
{
    /// <summary>
    /// Name of the processor
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Version of the processor
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the processor started
    /// </summary>
    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Host machine name where the processor is running
    /// </summary>
    [JsonPropertyName("hostName")]
    public string HostName { get; set; } = string.Empty;

    /// <summary>
    /// Process ID of the processor
    /// </summary>
    [JsonPropertyName("processId")]
    public int ProcessId { get; set; }

    /// <summary>
    /// Environment where the processor is running (Development, Production, etc.)
    /// </summary>
    [JsonPropertyName("environment")]
    public string Environment { get; set; } = string.Empty;
}

/// <summary>
/// Performance metrics for the processor
/// </summary>
public class ProcessorPerformanceMetrics
{
    /// <summary>
    /// Current CPU usage percentage (0-100)
    /// </summary>
    [JsonPropertyName("cpuUsagePercent")]
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Current memory usage in bytes
    /// </summary>
    [JsonPropertyName("memoryUsageBytes")]
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// Current memory usage in MB for easier reading
    /// </summary>
    [JsonPropertyName("memoryUsageMB")]
    public double MemoryUsageMB => MemoryUsageBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Total number of activities processed since startup
    /// </summary>
    [JsonPropertyName("totalActivitiesProcessed")]
    public long TotalActivitiesProcessed { get; set; }

    /// <summary>
    /// Number of successful activities processed
    /// </summary>
    [JsonPropertyName("successfulActivities")]
    public long SuccessfulActivities { get; set; }

    /// <summary>
    /// Number of failed activities processed
    /// </summary>
    [JsonPropertyName("failedActivities")]
    public long FailedActivities { get; set; }

    /// <summary>
    /// Activities processed per minute (throughput)
    /// </summary>
    [JsonPropertyName("activitiesPerMinute")]
    public double ActivitiesPerMinute { get; set; }

    /// <summary>
    /// Average activity execution time in milliseconds
    /// </summary>
    [JsonPropertyName("averageExecutionTimeMs")]
    public double AverageExecutionTimeMs { get; set; }

    /// <summary>
    /// Success rate percentage (0-100)
    /// </summary>
    [JsonPropertyName("successRatePercent")]
    public double SuccessRatePercent => 
        TotalActivitiesProcessed > 0 ? (SuccessfulActivities * 100.0) / TotalActivitiesProcessed : 100.0;

    /// <summary>
    /// Timestamp when these metrics were collected
    /// </summary>
    [JsonPropertyName("collectedAt")]
    public DateTime CollectedAt { get; set; }
}
