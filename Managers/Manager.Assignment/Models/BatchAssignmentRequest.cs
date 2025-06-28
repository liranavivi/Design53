using System.ComponentModel.DataAnnotations;

namespace Manager.Assignment.Models;

/// <summary>
/// Request model for batch assignment operations
/// </summary>
public class BatchAssignmentRequest
{
    /// <summary>
    /// List of assignment IDs to retrieve
    /// </summary>
    [Required]
    public List<Guid> AssignmentIds { get; set; } = new();
}

/// <summary>
/// Response model for batch assignment operations
/// </summary>
public class BatchAssignmentResponse
{
    /// <summary>
    /// Successfully retrieved assignments
    /// </summary>
    public List<Shared.Entities.AssignmentEntity> Assignments { get; set; } = new();

    /// <summary>
    /// IDs that were not found
    /// </summary>
    public List<Guid> NotFound { get; set; } = new();

    /// <summary>
    /// Total number of requested IDs
    /// </summary>
    public int RequestedCount { get; set; }

    /// <summary>
    /// Number of successfully retrieved assignments
    /// </summary>
    public int FoundCount { get; set; }
}
