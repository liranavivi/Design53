using Shared.Models;

namespace Shared.MassTransit.Commands;

/// <summary>
/// Command to execute an activity in the processor
/// </summary>
public class ExecuteActivityCommand
{
    /// <summary>
    /// ID of the processor that should handle this activity
    /// </summary>
    public Guid ProcessorId { get; set; }

    /// <summary>
    /// ID of the orchestrated flow entity
    /// </summary>
    public Guid OrchestratedFlowEntityId { get; set; }

    /// <summary>
    /// ID of the step being executed
    /// </summary>
    public Guid StepId { get; set; }

    /// <summary>
    /// Unique execution ID for this activity instance
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Collection of assignment models to process
    /// </summary>
    public List<AssignmentModel> Entities { get; set; } = new();

    /// <summary>
    /// Correlation ID for tracking (defaults to Guid.Empty)
    /// </summary>
    public Guid CorrelationId { get; set; } = Guid.Empty;

    /// <summary>
    /// Unique publish ID generated for each command publication (Guid.Empty for entry points)
    /// </summary>
    public Guid PublishId { get; set; } = Guid.Empty;

    /// <summary>
    /// Timestamp when the command was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional timeout for the activity execution
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Priority of the activity (higher numbers = higher priority)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Additional metadata for the activity
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}



/// <summary>
/// Command to get statistics for a processor
/// </summary>
public class GetStatisticsCommand
{
    /// <summary>
    /// ID of the processor to get statistics for
    /// </summary>
    public Guid ProcessorId { get; set; }

    /// <summary>
    /// Request ID for tracking
    /// </summary>
    public Guid RequestId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Start date for statistics period (null for all time)
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// End date for statistics period (null for current time)
    /// </summary>
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Timestamp when the request was made
    /// </summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Include detailed metrics breakdown
    /// </summary>
    public bool IncludeDetailedMetrics { get; set; } = false;
}
