namespace Shared.MassTransit.Events;

/// <summary>
/// Event published when a delivery entity is created.
/// </summary>
public class DeliveryCreatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the created delivery.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the delivery.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the delivery.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the delivery.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the payload of the delivery.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the schema identifier of the delivery.
    /// </summary>
    public Guid SchemaId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the delivery was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who created the delivery.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a delivery entity is updated.
/// </summary>
public class DeliveryUpdatedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the updated delivery.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the delivery.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the delivery.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the delivery.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the payload of the delivery.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the schema identifier of the delivery.
    /// </summary>
    public Guid SchemaId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the delivery was updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who updated the delivery.
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a delivery entity is deleted.
/// </summary>
public class DeliveryDeletedEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the deleted delivery.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the delivery was deleted.
    /// </summary>
    public DateTime DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who deleted the delivery.
    /// </summary>
    public string DeletedBy { get; set; } = string.Empty;
}
