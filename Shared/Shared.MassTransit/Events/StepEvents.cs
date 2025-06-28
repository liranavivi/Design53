using Shared.Entities.Enums;

namespace Shared.MassTransit.Events;

/// <summary>
/// Event published when a step entity is created.
/// </summary>
public class StepCreatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the created step.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the step.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the step.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the step.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the processor identifier that will execute this step.
    /// </summary>
    public Guid ProcessorId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets the collection of next step identifiers.
    /// </summary>
    public List<Guid> NextStepIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the entry condition for this step.
    /// </summary>
    public StepEntryCondition EntryCondition { get; set; } = StepEntryCondition.PreviousSuccess;

    /// <summary>
    /// Gets or sets the timestamp when the step was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who created the step.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a step entity is updated.
/// </summary>
public class StepUpdatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the updated step.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the step.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the step.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the step.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the processor identifier that will execute this step.
    /// </summary>
    public Guid ProcessorId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets the collection of next step identifiers.
    /// </summary>
    public List<Guid> NextStepIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the entry condition for this step.
    /// </summary>
    public StepEntryCondition EntryCondition { get; set; } = StepEntryCondition.PreviousSuccess;

    /// <summary>
    /// Gets or sets the timestamp when the step was updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who updated the step.
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a step entity is deleted.
/// </summary>
public class StepDeletedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the deleted step.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the step was deleted.
    /// </summary>
    public DateTime DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who deleted the step.
    /// </summary>
    public string DeletedBy { get; set; } = string.Empty;
}
