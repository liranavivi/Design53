using Manager.Orchestrator.Models;

namespace Manager.Orchestrator.Services;

/// <summary>
/// Interface for orchestration business logic service
/// </summary>
public interface IOrchestrationService
{
    /// <summary>
    /// Starts orchestration for the given orchestrated flow ID
    /// Retrieves all required data from managers and stores in cache
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID to start</param>
    /// <returns>Task representing the start operation</returns>
    Task StartOrchestrationAsync(Guid orchestratedFlowId);

    /// <summary>
    /// Stops orchestration for the given orchestrated flow ID
    /// Removes data from cache and performs cleanup
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID to stop</param>
    /// <returns>Task representing the stop operation</returns>
    Task StopOrchestrationAsync(Guid orchestratedFlowId);

    /// <summary>
    /// Gets orchestration status for the given orchestrated flow ID
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID to check</param>
    /// <returns>Orchestration status information</returns>
    Task<OrchestrationStatusModel> GetOrchestrationStatusAsync(Guid orchestratedFlowId);

    /// <summary>
    /// Gets health status for a specific processor
    /// </summary>
    /// <param name="processorId">The processor ID to check</param>
    /// <returns>Processor health status or null if not found</returns>
    Task<Shared.Models.ProcessorHealthResponse?> GetProcessorHealthAsync(Guid processorId);

    /// <summary>
    /// Gets health status for all processors in a specific orchestrated flow
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID to check</param>
    /// <returns>Dictionary of processor health statuses or null if orchestrated flow not found in cache</returns>
    Task<Shared.Models.ProcessorsHealthResponse?> GetProcessorsHealthByOrchestratedFlowAsync(Guid orchestratedFlowId);

    /// <summary>
    /// Validates the health of all processors required for orchestration
    /// </summary>
    /// <param name="processorIds">Collection of processor IDs to validate</param>
    /// <returns>True if all processors are healthy, false otherwise</returns>
    Task<bool> ValidateProcessorHealthForExecutionAsync(List<Guid> processorIds);
}

/// <summary>
/// Model representing orchestration status
/// </summary>
public class OrchestrationStatusModel
{
    /// <summary>
    /// The orchestrated flow ID
    /// </summary>
    public Guid OrchestratedFlowId { get; set; }

    /// <summary>
    /// Indicates if orchestration is active (data exists in cache)
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Timestamp when orchestration was started
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Timestamp when orchestration data expires
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Number of steps in the orchestration
    /// </summary>
    public int StepCount { get; set; }

    /// <summary>
    /// Number of assignments in the orchestration
    /// </summary>
    public int AssignmentCount { get; set; }
}
