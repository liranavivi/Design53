namespace Shared.MassTransit.Events;

/// <summary>
/// Event published when a workflow entity is created.
/// </summary>
public class WorkflowCreatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the created workflow.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the workflow.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the workflow.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the workflow.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of step IDs that belong to this workflow.
    /// </summary>
    public List<Guid> StepIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the timestamp when the workflow was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who created the workflow.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a workflow entity is updated.
/// </summary>
public class WorkflowUpdatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the updated workflow.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the workflow.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the workflow.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the workflow.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of step IDs that belong to this workflow.
    /// </summary>
    public List<Guid> StepIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the timestamp when the workflow was updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who updated the workflow.
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a workflow entity is deleted.
/// </summary>
public class WorkflowDeletedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the deleted workflow.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the workflow was deleted.
    /// </summary>
    public DateTime DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who deleted the workflow.
    /// </summary>
    public string DeletedBy { get; set; } = string.Empty;
}
