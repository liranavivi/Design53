using System.Diagnostics.Metrics;

namespace Shared.Processor.Services;

/// <summary>
/// Service for generating consistent processor metrics labels across all metric types.
/// Ensures all metrics use the same processor_composite_key and labeling standards.
/// </summary>
public interface IProcessorMetricsLabelService
{
    /// <summary>
    /// The standardized processor composite key (e.g., "1.1.1_FileProcessor")
    /// </summary>
    string ProcessorCompositeKey { get; }

    /// <summary>
    /// The processor instance ID (unique per processor instance)
    /// </summary>
    string ProcessorInstanceId { get; }

    /// <summary>
    /// Gets the standard base labels that should be included in all processor metrics
    /// </summary>
    /// <returns>KeyValuePair array with processor_composite_key, processor_name, processor_version, processor_id, environment</returns>
    KeyValuePair<string, object?>[] GetStandardLabels();

    /// <summary>
    /// Gets labels for activity processing metrics (workflow-related)
    /// </summary>
    /// <param name="correlationId">Correlation ID for the activity</param>
    /// <param name="executionId">Execution ID for the activity</param>
    /// <param name="stepId">Step ID for the activity</param>
    /// <param name="orchestratedFlowId">Orchestrated flow ID (optional)</param>
    /// <param name="status">Activity status (e.g., "success", "failed")</param>
    /// <returns>KeyValuePair array with standard labels plus activity-specific labels</returns>
    KeyValuePair<string, object?>[] GetActivityLabels(string correlationId, string executionId, string stepId, string? orchestratedFlowId = null, string status = "success");

    /// <summary>
    /// Gets labels for file processing metrics
    /// </summary>
    /// <param name="correlationId">Correlation ID for the file processing</param>
    /// <param name="fileType">Type of file being processed (e.g., "assignment_data")</param>
    /// <returns>KeyValuePair array with standard labels plus file-specific labels</returns>
    KeyValuePair<string, object?>[] GetFileLabels(string correlationId, string fileType);

    /// <summary>
    /// Gets labels for health check metrics
    /// </summary>
    /// <param name="healthCheckName">Name of the health check (e.g., "cache", "initialization")</param>
    /// <param name="healthCheckStatus">Status of the health check (e.g., "Healthy", "Unhealthy")</param>
    /// <returns>KeyValuePair array with standard labels plus health-specific labels</returns>
    KeyValuePair<string, object?>[] GetHealthLabels(string healthCheckName, string healthCheckStatus);

    /// <summary>
    /// Gets labels for system/performance metrics
    /// </summary>
    /// <returns>KeyValuePair array with standard labels for system metrics</returns>
    KeyValuePair<string, object?>[] GetSystemLabels();

    /// <summary>
    /// Gets labels for cache metrics
    /// </summary>
    /// <returns>KeyValuePair array with standard labels for cache metrics</returns>
    KeyValuePair<string, object?>[] GetCacheLabels();
}
