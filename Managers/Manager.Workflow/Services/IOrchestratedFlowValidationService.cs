using System;
using System.Threading.Tasks;

namespace Manager.Workflow.Services;

/// <summary>
/// Service for validating referential integrity with OrchestratedFlow entities
/// </summary>
public interface IOrchestratedFlowValidationService
{
    /// <summary>
    /// Check if any OrchestratedFlow entities reference the specified workflow ID
    /// </summary>
    /// <param name="workflowId">The workflow ID to check for references</param>
    /// <returns>True if any OrchestratedFlow entities reference the workflow, false otherwise</returns>
    Task<bool> CheckWorkflowReferencesAsync(Guid workflowId);
}
