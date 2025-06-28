namespace Shared.MassTransit.Events;

/// <summary>
/// Event published when a schema entity is created.
/// </summary>
public class SchemaCreatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the created schema.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the schema.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the schema.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the schema.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the definition of the schema.
    /// </summary>
    public string Definition { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the schema was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who created the schema.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a schema entity is updated.
/// </summary>
public class SchemaUpdatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the updated schema.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the schema.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the schema.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the schema.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the definition of the schema.
    /// </summary>
    public string Definition { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the schema was updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who updated the schema.
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a schema entity is deleted.
/// </summary>
public class SchemaDeletedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the deleted schema.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the schema was deleted.
    /// </summary>
    public DateTime DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who deleted the schema.
    /// </summary>
    public string DeletedBy { get; set; } = string.Empty;
}
