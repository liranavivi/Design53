using System;
using System.Threading.Tasks;

namespace Manager.Step.Services;

/// <summary>
/// Service for validating entity references before delete/update operations
/// </summary>
public interface IEntityReferenceValidator
{
    /// <summary>
    /// Validates that a step entity can be deleted by checking for references in other entities
    /// </summary>
    /// <param name="stepId">The step ID to validate</param>
    /// <exception cref="InvalidOperationException">Thrown if the step cannot be deleted due to existing references</exception>
    Task ValidateStepCanBeDeleted(Guid stepId);

    /// <summary>
    /// Validates that a step entity can be updated by checking for references in other entities
    /// </summary>
    /// <param name="stepId">The step ID to validate</param>
    /// <exception cref="InvalidOperationException">Thrown if the step cannot be updated due to existing references</exception>
    Task ValidateStepCanBeUpdated(Guid stepId);
}
