namespace Shared.MassTransit.Events;

/// <summary>
/// Event published when a processor entity is created.
/// </summary>
public class ProcessorCreatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the created processor.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the processor.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the processor.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the processor.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the input schema identifier of the processor.
    /// </summary>
    public Guid InputSchemaId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets the output schema identifier of the processor.
    /// </summary>
    public Guid OutputSchemaId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the processor was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who created the processor.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a processor entity is updated.
/// </summary>
public class ProcessorUpdatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the updated processor.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the processor.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the processor.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the processor.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the input schema identifier of the processor.
    /// </summary>
    public Guid InputSchemaId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets the output schema identifier of the processor.
    /// </summary>
    public Guid OutputSchemaId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the processor was updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who updated the processor.
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a processor entity is deleted.
/// </summary>
public class ProcessorDeletedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the deleted processor.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the processor was deleted.
    /// </summary>
    public DateTime DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who deleted the processor.
    /// </summary>
    public string DeletedBy { get; set; } = string.Empty;
}
