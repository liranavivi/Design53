using Shared.Models;
namespace Shared.MassTransit.Events;


/// <summary>
/// Event published when an activity is successfully executed
/// </summary>
public class ActivityExecutedEvent
{
    /// <summary>
    /// ID of the processor that executed the activity
    /// </summary>
    public Guid ProcessorId { get; init; }

    /// <summary>
    /// ID of the orchestrated flow entity
    /// </summary>
    public Guid OrchestratedFlowEntityId { get; init; }

    /// <summary>
    /// ID of the step that was executed
    /// </summary>
    public Guid StepId { get; init; }

    /// <summary>
    /// Execution ID for this activity instance
    /// </summary>
    public Guid ExecutionId { get; init; }

    /// <summary>
    /// Correlation ID for tracking (defaults to Guid.Empty)
    /// </summary>
    public Guid CorrelationId { get; init; } = Guid.Empty;

    /// <summary>
    /// Unique publish ID that was used for this execution (Guid.Empty for entry points)
    /// </summary>
    public Guid PublishId { get; init; } = Guid.Empty;

    /// <summary>
    /// Timestamp when the activity was executed
    /// </summary>
    public DateTime ExecutedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Duration of the activity execution
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Status of the execution
    /// </summary>
    public ActivityExecutionStatus Status { get; init; }

    /// <summary>
    /// Size of the result data in bytes
    /// </summary>
    public long ResultDataSize { get; init; }

    /// <summary>
    /// Number of entities processed
    /// </summary>
    public int EntitiesProcessed { get; init; }

}

/// <summary>
/// Event published when an activity execution fails
/// </summary>
public class ActivityFailedEvent
{
    /// <summary>
    /// ID of the processor that attempted to execute the activity
    /// </summary>
    public Guid ProcessorId { get; init; }

    /// <summary>
    /// ID of the orchestrated flow entity
    /// </summary>
    public Guid OrchestratedFlowEntityId { get; init; }

    /// <summary>
    /// ID of the step that failed
    /// </summary>
    public Guid StepId { get; init; }

    /// <summary>
    /// Execution ID for this activity instance
    /// </summary>
    public Guid ExecutionId { get; init; }

    /// <summary>
    /// Optional correlation ID for tracking
    /// </summary>
    public Guid CorrelationId { get; init; }

    /// <summary>
    /// Unique publish ID that was used for this execution (Guid.Empty for entry points)
    /// </summary>
    public Guid PublishId { get; init; } = Guid.Empty;

    /// <summary>
    /// Timestamp when the activity failed
    /// </summary>
    public DateTime FailedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Duration before the activity failed
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Error message describing the failure
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Exception type that caused the failure
    /// </summary>
    public string? ExceptionType { get; init; }

    /// <summary>
    /// Stack trace of the exception (if available)
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// Number of entities that were being processed when the failure occurred
    /// </summary>
    public int EntitiesBeingProcessed { get; init; }

    /// <summary>
    /// Whether this was a validation failure
    /// </summary>
    public bool IsValidationFailure { get; init; }
}
