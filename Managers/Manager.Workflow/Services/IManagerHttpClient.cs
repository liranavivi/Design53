using System;
using System.Threading.Tasks;

namespace Manager.Workflow.Services;

/// <summary>
/// HTTP client interface for communicating with other managers for validation
/// </summary>
public interface IManagerHttpClient
{
    /// <summary>
    /// Check if a step exists in the Step Manager
    /// </summary>
    /// <param name="stepId">The step ID to check</param>
    /// <returns>True if step exists, false otherwise</returns>
    Task<bool> CheckStepExists(Guid stepId);
}
