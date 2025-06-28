using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Manager.Workflow.Services;

/// <summary>
/// Service interface for validating Workflow entity references
/// </summary>
public interface IWorkflowValidationService
{
    /// <summary>
    /// Validates that all step IDs exist in the Step Manager
    /// </summary>
    /// <param name="stepIds">The list of step IDs to validate</param>
    /// <returns>Task that completes when validation is done</returns>
    /// <exception cref="InvalidOperationException">Thrown when any step doesn't exist or validation service is unavailable</exception>
    Task ValidateStepsExist(List<Guid> stepIds);
}
