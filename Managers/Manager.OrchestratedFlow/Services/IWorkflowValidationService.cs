using System;
using System.Threading.Tasks;

namespace Manager.OrchestratedFlow.Services;

/// <summary>
/// Service for validating references to Workflow entities
/// </summary>
public interface IWorkflowValidationService
{
    /// <summary>
    /// Check if the specified workflow ID exists
    /// </summary>
    /// <param name="workflowId">The workflow ID to validate</param>
    /// <returns>True if the workflow exists, false otherwise</returns>
    Task<bool> ValidateWorkflowExistsAsync(Guid workflowId);
}
