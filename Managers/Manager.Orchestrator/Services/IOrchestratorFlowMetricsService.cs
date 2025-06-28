namespace Manager.Orchestrator.Services;

/// <summary>
/// Service for recording orchestrator flow metrics optimized for anomaly detection.
/// Follows the processor pattern with focused metrics: consume counter, publish counter, and anomaly detection.
/// Reduces metric volume while focusing on important operational issues.
/// </summary>
public interface IOrchestratorFlowMetricsService : IDisposable
{
    /// <summary>
    /// Records ExecuteActivityCommand consumption metrics (activity events consumed by orchestrator)
    /// </summary>
    /// <param name="success">Whether the command was consumed successfully</param>
    /// <param name="orchestratedFlowId">The orchestrated flow ID for filtering (optional)</param>
    void RecordCommandConsumed(bool success, Guid? orchestratedFlowId = null);

    /// <summary>
    /// Records activity event publishing metrics (step commands published by orchestrator)
    /// </summary>
    /// <param name="success">Whether the event was published successfully</param>
    /// <param name="eventType">Type of event published</param>
    /// <param name="orchestratedFlowId">The orchestrated flow ID for filtering (optional)</param>
    void RecordEventPublished(bool success, string eventType, Guid? orchestratedFlowId = null);

    /// <summary>
    /// Records flow anomaly detection metrics
    /// </summary>
    /// <param name="consumedCount">Number of commands consumed</param>
    /// <param name="publishedCount">Number of events published</param>
    /// <param name="orchestratedFlowId">The orchestrated flow ID for filtering (optional)</param>
    void RecordFlowAnomaly(long consumedCount, long publishedCount, Guid? orchestratedFlowId = null);
}
