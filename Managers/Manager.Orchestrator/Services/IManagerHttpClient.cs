using Shared.Models;
using Shared.Entities;

namespace Manager.Orchestrator.Services;

/// <summary>
/// Interface for HTTP communication with other entity managers
/// </summary>
public interface IManagerHttpClient
{
    /// <summary>
    /// Retrieves the orchestrated flow entity by ID
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID</param>
    /// <returns>The orchestrated flow entity</returns>
    Task<OrchestratedFlowEntity?> GetOrchestratedFlowAsync(Guid orchestratedFlowId);

    /// <summary>
    /// Retrieves all step-related data for the orchestrated flow
    /// </summary>
    /// <param name="workflowId">The workflow ID from the orchestrated flow</param>
    /// <returns>Step manager model with all step data</returns>
    Task<StepManagerModel> GetStepManagerDataAsync(Guid workflowId);

    /// <summary>
    /// Retrieves all assignment-related data for the orchestrated flow
    /// </summary>
    /// <param name="assignmentIds">List of assignment IDs from the orchestrated flow</param>
    /// <returns>Assignment manager model with all assignment data</returns>
    Task<AssignmentManagerModel> GetAssignmentManagerDataAsync(List<Guid> assignmentIds);

    /// <summary>
    /// Retrieves schema definition by schema ID
    /// </summary>
    /// <param name="schemaId">The schema ID</param>
    /// <returns>Schema definition string</returns>
    Task<string> GetSchemaDefinitionAsync(Guid schemaId);
}
