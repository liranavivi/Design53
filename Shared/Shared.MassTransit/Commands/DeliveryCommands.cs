namespace Shared.MassTransit.Commands;

/// <summary>
/// Command to create a new delivery entity.
/// </summary>
public class CreateDeliveryCommand
{
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
    /// Gets or sets the user who requested the creation.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to update an existing delivery entity.
/// </summary>
public class UpdateDeliveryCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the delivery to update.
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
    /// Gets or sets the user who requested the update.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to delete a delivery entity.
/// </summary>
public class DeleteDeliveryCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the delivery to delete.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the deletion.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Query to retrieve a delivery entity.
/// </summary>
public class GetDeliveryQuery
{
    /// <summary>
    /// Gets or sets the unique identifier of the delivery to retrieve.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Gets or sets the composite key of the delivery to retrieve.
    /// </summary>
    public string? CompositeKey { get; set; }
}


