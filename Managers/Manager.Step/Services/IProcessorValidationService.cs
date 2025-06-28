using System;
using System.Threading.Tasks;

namespace Manager.Step.Services;

/// <summary>
/// Interface for validating processor and step references during step entity operations
/// </summary>
public interface IProcessorValidationService
{
    /// <summary>
    /// Validates that a processor exists before creating or updating a step entity
    /// </summary>
    /// <param name="processorId">The processor ID to validate</param>
    /// <returns>Task that completes successfully if processor exists</returns>
    /// <exception cref="InvalidOperationException">Thrown if processor doesn't exist or validation fails</exception>
    Task ValidateProcessorExists(Guid processorId);

    /// <summary>
    /// Validates that all next step IDs exist before creating or updating a step entity
    /// </summary>
    /// <param name="nextStepIds">The list of next step IDs to validate</param>
    /// <returns>Task that completes successfully if all next steps exist</returns>
    /// <exception cref="InvalidOperationException">Thrown if any next step doesn't exist or validation fails</exception>
    Task ValidateNextStepsExist(List<Guid> nextStepIds);
}
