namespace Shared.MassTransit.Commands;

/// <summary>
/// Command to create a new schema entity.
/// </summary>
public class CreateSchemaCommand
{
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
    /// Gets or sets the user who requested the creation.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to update an existing schema entity.
/// </summary>
public class UpdateSchemaCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the schema to update.
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
    /// Gets or sets the user who requested the update.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to delete a schema entity.
/// </summary>
public class DeleteSchemaCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the schema to delete.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the deletion.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Query to retrieve a schema entity.
/// </summary>
public class GetSchemaQuery
{
    /// <summary>
    /// Gets or sets the unique identifier of the schema to retrieve.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Gets or sets the composite key of the schema to retrieve.
    /// </summary>
    public string? CompositeKey { get; set; }
}

/// <summary>
/// Query to retrieve the definition of a schema entity.
/// </summary>
public class GetSchemaDefinitionQuery
{
    /// <summary>
    /// Gets or sets the unique identifier of the schema.
    /// </summary>
    public Guid SchemaId { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the definition.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Response for the GetSchemaDefinitionQuery.
/// </summary>
public class GetSchemaDefinitionQueryResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the query was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the schema definition.
    /// </summary>
    public string? Definition { get; set; }

    /// <summary>
    /// Gets or sets the response message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
