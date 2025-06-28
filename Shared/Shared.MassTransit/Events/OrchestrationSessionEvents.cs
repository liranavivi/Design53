namespace Shared.MassTransit.Events;

/// <summary>
/// Event published when a orchestrationsession entity is created.
/// </summary>
public class OrchestrationSessionCreatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the created orchestrationsession.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the orchestrationsession.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the orchestrationsession.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the orchestrationsession.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the definition of the orchestrationsession.
    /// </summary>
    public string Definition { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the orchestrationsession was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who created the orchestrationsession.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a orchestrationsession entity is updated.
/// </summary>
public class OrchestrationSessionUpdatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the updated orchestrationsession.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the orchestrationsession.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the orchestrationsession.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the orchestrationsession.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the definition of the orchestrationsession.
    /// </summary>
    public string Definition { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the orchestrationsession was updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who updated the orchestrationsession.
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a orchestrationsession entity is deleted.
/// </summary>
public class OrchestrationSessionDeletedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the deleted orchestrationsession.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the orchestrationsession was deleted.
    /// </summary>
    public DateTime DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who deleted the orchestrationsession.
    /// </summary>
    public string DeletedBy { get; set; } = string.Empty;
}
