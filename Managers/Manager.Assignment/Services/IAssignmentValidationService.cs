using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Manager.Assignment.Services;

/// <summary>
/// Service interface for validating Assignment entity references
/// </summary>
public interface IAssignmentValidationService
{
    /// <summary>
    /// Validates that a step exists in the Step Manager
    /// </summary>
    /// <param name="stepId">The step ID to validate</param>
    /// <returns>Task that completes when validation is done</returns>
    /// <exception cref="InvalidOperationException">Thrown when step doesn't exist or validation service is unavailable</exception>
    Task ValidateStepExists(Guid stepId);

    /// <summary>
    /// Validates that all entity IDs exist in their respective managers
    /// </summary>
    /// <param name="entityIds">The list of entity IDs to validate</param>
    /// <returns>Task that completes when validation is done</returns>
    /// <exception cref="InvalidOperationException">Thrown when any entity doesn't exist or validation service is unavailable</exception>
    Task ValidateEntitiesExist(List<Guid> entityIds);
}
