using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Manager.OrchestratedFlow.Services;

/// <summary>
/// Service for validating references to Assignment entities
/// </summary>
public interface IAssignmentValidationService
{
    /// <summary>
    /// Check if the specified assignment ID exists
    /// </summary>
    /// <param name="assignmentId">The assignment ID to validate</param>
    /// <returns>True if the assignment exists, false otherwise</returns>
    Task<bool> ValidateAssignmentExistsAsync(Guid assignmentId);

    /// <summary>
    /// Check if all specified assignment IDs exist
    /// </summary>
    /// <param name="assignmentIds">The assignment IDs to validate</param>
    /// <returns>True if all assignments exist, false otherwise</returns>
    Task<bool> ValidateAssignmentsExistAsync(IEnumerable<Guid> assignmentIds);
}
