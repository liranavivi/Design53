namespace Shared.MassTransit.Events;

/// <summary>
/// Event published when a assignment entity is created.
/// </summary>
public class AssignmentCreatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the created assignment.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the assignment.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the assignment.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the assignment.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the step identifier of the assignment.
    /// </summary>
    public Guid StepId { get; set; }

    /// <summary>
    /// Gets or sets the entity identifiers of the assignment.
    /// </summary>
    public List<Guid> EntityIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the timestamp when the assignment was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who created the assignment.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a assignment entity is updated.
/// </summary>
public class AssignmentUpdatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the updated assignment.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the assignment.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the assignment.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the assignment.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the step identifier of the assignment.
    /// </summary>
    public Guid StepId { get; set; }

    /// <summary>
    /// Gets or sets the entity identifiers of the assignment.
    /// </summary>
    public List<Guid> EntityIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the timestamp when the assignment was updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who updated the assignment.
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a assignment entity is deleted.
/// </summary>
public class AssignmentDeletedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the deleted assignment.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the assignment was deleted.
    /// </summary>
    public DateTime DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who deleted the assignment.
    /// </summary>
    public string DeletedBy { get; set; } = string.Empty;
}
