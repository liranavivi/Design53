using System;
using System.Threading.Tasks;

namespace Manager.Assignment.Services;

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

    /// <summary>
    /// Check if an entity exists in the appropriate manager
    /// For now, this will check if it's a valid GUID format and assume it exists
    /// In a full implementation, this would route to the appropriate manager based on entity type
    /// </summary>
    /// <param name="entityId">The entity ID to check</param>
    /// <returns>True if entity exists, false otherwise</returns>
    Task<bool> CheckEntityExists(Guid entityId);
}
