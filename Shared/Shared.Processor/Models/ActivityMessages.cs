using Shared.Models;

namespace Shared.Processor.Models;

/// <summary>
/// Message for executing an activity in the processor
/// </summary>
public class ProcessorActivityMessage
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
    /// ID of the previous step that led to this step execution (Guid.Empty for entry points)
    /// </summary>
    public Guid PreviousStepId { get; set; } = Guid.Empty;

    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Response message after processing an activity
/// </summary>
public class ProcessorActivityResponse
{
    /// <summary>
    /// ID of the processor that handled this activity
    /// </summary>
    public Guid ProcessorId { get; set; }

    /// <summary>
    /// ID of the orchestrated flow entity
    /// </summary>
    public Guid OrchestratedFlowEntityId { get; set; }

    /// <summary>
    /// ID of the step that was executed
    /// </summary>
    public Guid StepId { get; set; }

    /// <summary>
    /// Execution ID for this activity instance
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Status of the activity execution
    /// </summary>
    public ActivityExecutionStatus Status { get; set; }

    /// <summary>
    /// Correlation ID for tracking (defaults to Guid.Empty)
    /// </summary>
    public Guid CorrelationId { get; set; } = Guid.Empty;

    /// <summary>
    /// Optional error message if execution failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Timestamp when the activity was completed
    /// </summary>
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Duration of the activity execution
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Result returned from ExecuteActivityAsync containing all ProcessedActivityData info plus serialized data
/// </summary>
public class ActivityExecutionResult
{
    /// <summary>
    /// Result message from processing
    /// </summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>
    /// Status of the processing
    /// </summary>
    public ActivityExecutionStatus Status { get; set; } = ActivityExecutionStatus.Processing;

    /// <summary>
    /// Duration of the processing
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Name of the processor that handled this activity
    /// </summary>
    public string ProcessorName { get; set; } = string.Empty;

    /// <summary>
    /// Version of the processor
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Execution ID
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// JSON serialized string of the Data property only
    /// </summary>
    public string SerializedData { get; set; } = string.Empty;
}
