using System;
using System.Threading.Tasks;

namespace Manager.Assignment.Services;

/// <summary>
/// Service for validating referential integrity with OrchestratedFlow entities
/// </summary>
public interface IOrchestratedFlowValidationService
{
    /// <summary>
    /// Check if any OrchestratedFlow entities reference the specified assignment ID
    /// </summary>
    /// <param name="assignmentId">The assignment ID to check for references</param>
    /// <returns>True if any OrchestratedFlow entities reference the assignment, false otherwise</returns>
    Task<bool> CheckAssignmentReferencesAsync(Guid assignmentId);
}
