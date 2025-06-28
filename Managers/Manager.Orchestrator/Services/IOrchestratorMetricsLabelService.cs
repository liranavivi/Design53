using System.Diagnostics.Metrics;

namespace Manager.Orchestrator.Services;

/// <summary>
/// Service for generating consistent orchestrator metrics labels across all metric types.
/// Ensures all metrics use the same orchestrator_composite_key and labeling standards.
/// Follows the same design pattern as ProcessorMetricsLabelService for consistency.
/// </summary>
public interface IOrchestratorMetricsLabelService
{
    /// <summary>
    /// The standardized orchestrator composite key (e.g., "1.0.0_OrchestratorManager")
    /// </summary>
    string OrchestratorCompositeKey { get; }

    /// <summary>
    /// The orchestrator instance ID (unique per orchestrator instance)
    /// </summary>
    string OrchestratorInstanceId { get; }

    /// <summary>
    /// Gets the standard base labels that should be included in all orchestrator metrics
    /// </summary>
    /// <returns>KeyValuePair array with orchestrator_composite_key, orchestrator_name, orchestrator_version, orchestrator_id, environment</returns>
    KeyValuePair<string, object?>[] GetStandardLabels();

    /// <summary>
    /// Gets labels for step command publishing metrics
    /// </summary>
    /// <param name="stepVersionName">Step version name (e.g., "1.1.0_DataValidationStep")</param>
    /// <param name="operation">Operation type (e.g., "publish_step_command")</param>
    /// <param name="status">Operation status (e.g., "success", "failed")</param>
    /// <returns>KeyValuePair array with standard labels plus step-specific labels</returns>
    KeyValuePair<string, object?>[] GetStepPublishLabels(string stepVersionName, string operation = "publish_step_command", string status = "success");

    /// <summary>
    /// Gets labels for activity event consumption metrics
    /// </summary>
    /// <param name="stepVersionName">Step version name (e.g., "1.1.0_DataValidationStep")</param>
    /// <param name="orchestratedFlowVersionName">Orchestrated flow version name (e.g., "2.0.0_DataProcessingFlow")</param>
    /// <param name="operation">Operation type (e.g., "process_activity_event")</param>
    /// <param name="status">Operation status (e.g., "success", "failed")</param>
    /// <returns>KeyValuePair array with standard labels plus consumption-specific labels</returns>
    KeyValuePair<string, object?>[] GetStepConsumptionLabels(string stepVersionName, string orchestratedFlowVersionName, string operation = "process_activity_event", string status = "success");

    /// <summary>
    /// Gets labels for orchestration flow metrics
    /// </summary>
    /// <param name="orchestratedFlowVersionName">Orchestrated flow version name (e.g., "2.0.0_DataProcessingFlow")</param>
    /// <param name="operation">Operation type (e.g., "execute_entry_points", "flow_completion")</param>
    /// <param name="status">Operation status (e.g., "success", "failed")</param>
    /// <returns>KeyValuePair array with standard labels plus flow-specific labels</returns>
    KeyValuePair<string, object?>[] GetFlowLabels(string orchestratedFlowVersionName, string operation, string status = "success");

    /// <summary>
    /// Gets labels for cache operation metrics
    /// </summary>
    /// <param name="operationType">Type of cache operation (e.g., "read", "write", "delete")</param>
    /// <param name="status">Operation status (e.g., "success", "failed")</param>
    /// <returns>KeyValuePair array with standard labels plus cache-specific labels</returns>
    KeyValuePair<string, object?>[] GetCacheLabels(string operationType, string status = "success");

    /// <summary>
    /// Gets labels for aggregated cache metrics (following processor pattern)
    /// </summary>
    /// <returns>KeyValuePair array with standard labels for cache metrics</returns>
    KeyValuePair<string, object?>[] GetCacheLabels();

    /// <summary>
    /// Gets labels for health check metrics
    /// </summary>
    /// <param name="healthCheckName">Name of the health check (e.g., "cache", "messagebus")</param>
    /// <param name="healthCheckStatus">Status of the health check (e.g., "Healthy", "Unhealthy")</param>
    /// <returns>KeyValuePair array with standard labels plus health-specific labels</returns>
    KeyValuePair<string, object?>[] GetHealthLabels(string healthCheckName, string healthCheckStatus);

    /// <summary>
    /// Gets labels for system/performance metrics
    /// </summary>
    /// <returns>KeyValuePair array with standard labels for system metrics</returns>
    KeyValuePair<string, object?>[] GetSystemLabels();
}
