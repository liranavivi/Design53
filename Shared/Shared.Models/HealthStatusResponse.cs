using System.Text.Json.Serialization;

namespace Shared.Models;

/// <summary>
/// Unified health status response for processor
/// </summary>
public class ProcessorHealthResponse
{
    /// <summary>
    /// ID of the correlation
    /// </summary>
    [JsonPropertyName("correlationId")]
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// ID of the healthCheck
    /// </summary>
    [JsonPropertyName("healthCheckId")]
    public Guid HealthCheckId { get; set; }
    
    /// <summary>
    /// ID of the processor
    /// </summary>
    [JsonPropertyName("processorId")]
    public Guid ProcessorId { get; set; }

    /// <summary>
    /// Overall health status
    /// </summary>
    [JsonPropertyName("status")]
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Detailed health message
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when health was last updated as Unix time
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public long LastUpdated { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    /// Health check interval in seconds from ProcessorHealthMonitor configuration.
    /// Indicates how frequently health checks are performed for this processor.
    /// </summary>
    [JsonPropertyName("healthCheckInterval")]
    public int HealthCheckInterval { get; set; }

    /// <summary>
    /// Timestamp when this health entry expires (optional for internal use)
    /// </summary>
    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// ID of the pod that reported this health status (optional for internal use)
    /// </summary>
    [JsonPropertyName("reportingPodId")]
    public string? ReportingPodId { get; set; }

    /// <summary>
    /// Processor uptime since last restart
    /// </summary>
    [JsonPropertyName("uptime")]
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Processor metadata information
    /// </summary>
    [JsonPropertyName("metadata")]
    public ProcessorMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Performance metrics for the processor (optional)
    /// </summary>
    [JsonPropertyName("performanceMetrics")]
    public ProcessorPerformanceMetrics? PerformanceMetrics { get; set; }

    /// <summary>
    /// Detailed health check results
    /// </summary>
    [JsonPropertyName("healthChecks")]
    public Dictionary<string, HealthCheckResult> HealthChecks { get; set; } = new();

    /// <summary>
    /// Indicates if this health entry is still valid based on TTL
    /// </summary>
    [JsonPropertyName("isExpired")]
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

    /// <summary>
    /// Timestamp when this response was generated (optional for API responses)
    /// </summary>
    [JsonPropertyName("retrievedAt")]
    public DateTime? RetrievedAt { get; set; }
}

/// <summary>
/// Response model for multiple processors health status
/// </summary>
public class ProcessorsHealthResponse
{
    /// <summary>
    /// The orchestrated flow ID this health check is for
    /// </summary>
    [JsonPropertyName("orchestratedFlowId")]
    public Guid OrchestratedFlowId { get; set; }

    /// <summary>
    /// Dictionary of processor health statuses with processor ID as key
    /// </summary>
    [JsonPropertyName("processors")]
    public Dictionary<Guid, ProcessorHealthResponse> Processors { get; set; } = new();

    /// <summary>
    /// Summary of overall health status
    /// </summary>
    [JsonPropertyName("summary")]
    public ProcessorsHealthSummary Summary { get; set; } = new();

    /// <summary>
    /// Timestamp when this response was generated
    /// </summary>
    [JsonPropertyName("retrievedAt")]
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Summary of processors health status
/// </summary>
public class ProcessorsHealthSummary
{
    /// <summary>
    /// Total number of processors
    /// </summary>
    [JsonPropertyName("totalProcessors")]
    public int TotalProcessors { get; set; }

    /// <summary>
    /// Number of healthy processors
    /// </summary>
    [JsonPropertyName("healthyProcessors")]
    public int HealthyProcessors { get; set; }

    /// <summary>
    /// Number of degraded processors
    /// </summary>
    [JsonPropertyName("degradedProcessors")]
    public int DegradedProcessors { get; set; }

    /// <summary>
    /// Number of unhealthy processors
    /// </summary>
    [JsonPropertyName("unhealthyProcessors")]
    public int UnhealthyProcessors { get; set; }

    /// <summary>
    /// Number of processors with no health data
    /// </summary>
    [JsonPropertyName("noHealthDataProcessors")]
    public int NoHealthDataProcessors { get; set; }

    /// <summary>
    /// Overall health status based on all processors
    /// </summary>
    [JsonPropertyName("overallStatus")]
    public HealthStatus OverallStatus { get; set; }

    /// <summary>
    /// List of processor IDs that are unhealthy or have no health data
    /// </summary>
    [JsonPropertyName("problematicProcessors")]
    public List<Guid> ProblematicProcessors { get; set; } = new();
}

/// <summary>
/// Statistics response for processor
/// </summary>
public class ProcessorStatisticsResponse
{
    /// <summary>
    /// ID of the processor
    /// </summary>
    public Guid ProcessorId { get; set; }

    /// <summary>
    /// Total number of activities processed
    /// </summary>
    public long TotalActivitiesProcessed { get; set; }

    /// <summary>
    /// Number of successful activities
    /// </summary>
    public long SuccessfulActivities { get; set; }

    /// <summary>
    /// Number of failed activities
    /// </summary>
    public long FailedActivities { get; set; }

    /// <summary>
    /// Average execution time for activities
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Start of the statistics period
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// End of the statistics period
    /// </summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// When these statistics were collected
    /// </summary>
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metrics
    /// </summary>
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}

/// <summary>
/// Health status enumeration
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Service is healthy and operational
    /// </summary>
    Healthy,

    /// <summary>
    /// Service is degraded but still operational
    /// </summary>
    Degraded,

    /// <summary>
    /// Service is unhealthy and may not be operational
    /// </summary>
    Unhealthy
}

/// <summary>
/// Individual health check result
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Status of this specific health check
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Description of the health check
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Additional data for this health check
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Duration of the health check
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Exception details if the health check failed
    /// </summary>
    public string? Exception { get; set; }
}
