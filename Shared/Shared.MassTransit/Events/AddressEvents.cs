namespace Shared.MassTransit.Events;

/// <summary>
/// Event published when a address entity is created.
/// </summary>
public class AddressCreatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the created address.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the address.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the address.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the address.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection string value.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the payload.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the schema identifier.
    /// </summary>
    public Guid SchemaId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the address was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who created the address.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a address entity is updated.
/// </summary>
public class AddressUpdatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the updated address.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the address.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the address.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the address.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection string value.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the configuration dictionary.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the schema identifier.
    /// </summary>
    public Guid SchemaId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the address was updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who updated the address.
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a address entity is deleted.
/// </summary>
public class AddressDeletedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the deleted address.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the address was deleted.
    /// </summary>
    public DateTime DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who deleted the address.
    /// </summary>
    public string DeletedBy { get; set; } = string.Empty;
}
