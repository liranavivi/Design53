namespace Shared.MassTransit.Events;

/// <summary>
/// Event published when a orchestratedflow entity is created.
/// </summary>
public class OrchestratedFlowCreatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the created orchestratedflow.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the orchestratedflow.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the orchestratedflow.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the orchestratedflow.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow identifier.
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// Gets or sets the assignment identifiers.
    /// </summary>
    public List<Guid> AssignmentIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the timestamp when the orchestratedflow was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who created the orchestratedflow.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a orchestratedflow entity is updated.
/// </summary>
public class OrchestratedFlowUpdatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the updated orchestratedflow.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the orchestratedflow.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the orchestratedflow.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the orchestratedflow.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow identifier.
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// Gets or sets the assignment identifiers.
    /// </summary>
    public List<Guid> AssignmentIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the timestamp when the orchestratedflow was updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who updated the orchestratedflow.
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a orchestratedflow entity is deleted.
/// </summary>
public class OrchestratedFlowDeletedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the deleted orchestratedflow.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the orchestratedflow was deleted.
    /// </summary>
    public DateTime DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who deleted the orchestratedflow.
    /// </summary>
    public string DeletedBy { get; set; } = string.Empty;
}
