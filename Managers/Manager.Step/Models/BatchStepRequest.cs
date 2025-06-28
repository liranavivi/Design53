using System.ComponentModel.DataAnnotations;

namespace Manager.Step.Models;

/// <summary>
/// Request model for batch step operations
/// </summary>
public class BatchStepRequest
{
    /// <summary>
    /// List of step IDs to retrieve
    /// </summary>
    [Required]
    public List<Guid> StepIds { get; set; } = new();
}

/// <summary>
/// Response model for batch step operations
/// </summary>
public class BatchStepResponse
{
    /// <summary>
    /// Successfully retrieved steps
    /// </summary>
    public List<Shared.Entities.StepEntity> Steps { get; set; } = new();

    /// <summary>
    /// IDs that were not found
    /// </summary>
    public List<Guid> NotFound { get; set; } = new();

    /// <summary>
    /// Total number of requested IDs
    /// </summary>
    public int RequestedCount { get; set; }

    /// <summary>
    /// Number of successfully retrieved steps
    /// </summary>
    public int FoundCount { get; set; }
}
