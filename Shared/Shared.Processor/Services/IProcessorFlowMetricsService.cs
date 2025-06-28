namespace Shared.Processor.Services;

/// <summary>
/// Service for recording processor flow metrics optimized for anomaly detection.
/// Follows the orchestrator pattern with focused metrics: consume counter, publish counter, and anomaly detection.
/// Reduces metric volume while focusing on important operational issues.
/// </summary>
public interface IProcessorFlowMetricsService : IDisposable
{
    /// <summary>
    /// Records ExecuteActivityCommand consumption metrics
    /// </summary>
    /// <param name="success">Whether the command was consumed successfully</param>
    /// <param name="processorName">Processor name for labeling</param>
    /// <param name="processorVersion">Processor version for labeling</param>
    /// <param name="orchestratedFlowId">The orchestrated flow ID for filtering (optional)</param>
    void RecordCommandConsumed(bool success, string processorName, string processorVersion, Guid? orchestratedFlowId = null);

    /// <summary>
    /// Records activity event publishing metrics (ActivityExecutedEvent or ActivityFailedEvent)
    /// </summary>
    /// <param name="success">Whether the event was published successfully</param>
    /// <param name="eventType">Type of event published (e.g., "ActivityExecutedEvent", "ActivityFailedEvent")</param>
    /// <param name="processorName">Processor name for labeling</param>
    /// <param name="processorVersion">Processor version for labeling</param>
    /// <param name="orchestratedFlowId">The orchestrated flow ID for filtering (optional)</param>
    void RecordEventPublished(bool success, string eventType, string processorName, string processorVersion, Guid? orchestratedFlowId = null);

    /// <summary>
    /// Records flow anomaly detection metrics
    /// </summary>
    /// <param name="processorName">Processor name for labeling</param>
    /// <param name="processorVersion">Processor version for labeling</param>
    /// <param name="consumedCount">Number of commands consumed</param>
    /// <param name="publishedCount">Number of events published</param>
    /// <param name="orchestratedFlowId">The orchestrated flow ID for filtering (optional)</param>
    void RecordFlowAnomaly(string processorName, string processorVersion, long consumedCount, long publishedCount, Guid? orchestratedFlowId = null);

}
