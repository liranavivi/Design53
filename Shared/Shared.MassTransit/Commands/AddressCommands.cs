namespace Shared.MassTransit.Commands;

/// <summary>
/// Command to create a new address entity.
/// </summary>
public class CreateAddressCommand
{
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
    /// Gets or sets the user who requested the creation.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to update an existing address entity.
/// </summary>
public class UpdateAddressCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the address to update.
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
    /// Gets or sets the user who requested the update.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to delete a address entity.
/// </summary>
public class DeleteAddressCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the address to delete.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the deletion.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Query to retrieve a address entity.
/// </summary>
public class GetAddressQuery
{
    /// <summary>
    /// Gets or sets the unique identifier of the address to retrieve.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Gets or sets the composite key of the address to retrieve.
    /// </summary>
    public string? CompositeKey { get; set; }
}

/// <summary>
/// Query to retrieve the payload of a address entity.
/// </summary>
public class GetAddressPayloadQuery
{
    /// <summary>
    /// Gets or sets the unique identifier of the address.
    /// </summary>
    public Guid AddressId { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the configuration.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Response for the GetAddressPayloadQuery.
/// </summary>
public class GetAddressPayloadQueryResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the query was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the payload.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the response message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
