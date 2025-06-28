using System;
using System.Threading.Tasks;

namespace Manager.Step.Services;

/// <summary>
/// HTTP client interface for communication with other entity managers
/// </summary>
public interface IManagerHttpClient
{
    /// <summary>
    /// Check if any assignment entities reference the specified step ID
    /// </summary>
    /// <param name="stepId">The step ID to check for references</param>
    /// <returns>True if any assignment entities reference the step, false otherwise</returns>
    Task<bool> CheckAssignmentStepReferences(Guid stepId);

    /// <summary>
    /// Check if any workflow entities reference the specified step ID
    /// </summary>
    /// <param name="stepId">The step ID to check for references</param>
    /// <returns>True if any workflow entities reference the step, false otherwise</returns>
    Task<bool> CheckWorkflowStepReferences(Guid stepId);

    /// <summary>
    /// Check if a processor exists in the Processor Manager
    /// </summary>
    /// <param name="processorId">The processor ID to check</param>
    /// <returns>True if processor exists, false otherwise</returns>
    Task<bool> CheckProcessorExists(Guid processorId);

    /// <summary>
    /// Check if a step exists in the Step Manager
    /// </summary>
    /// <param name="stepId">The step ID to check</param>
    /// <returns>True if step exists, false otherwise</returns>
    Task<bool> CheckStepExists(Guid stepId);
}
