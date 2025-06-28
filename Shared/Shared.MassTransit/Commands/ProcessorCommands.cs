using Shared.Entities;

namespace Shared.MassTransit.Commands;

/// <summary>
/// Command to create a new processor entity.
/// </summary>
public class CreateProcessorCommand
{
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
    /// Gets or sets the SHA-256 hash of the processor implementation.
    /// Used for runtime integrity validation to ensure version consistency.
    /// </summary>
    public string ImplementationHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user who requested the creation.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to update an existing processor entity.
/// </summary>
public class UpdateProcessorCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the processor to update.
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
    /// Gets or sets the user who requested the update.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to delete a processor entity.
/// </summary>
public class DeleteProcessorCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the processor to delete.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the deletion.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Query to retrieve a processor entity.
/// </summary>
public class GetProcessorQuery
{
    /// <summary>
    /// Gets or sets the unique identifier of the processor to retrieve.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Gets or sets the composite key of the processor to retrieve.
    /// </summary>
    public string? CompositeKey { get; set; }
}

/// <summary>
/// Response for the GetProcessorQuery.
/// </summary>
public class GetProcessorQueryResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the query was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the processor entity if found.
    /// </summary>
    public ProcessorEntity? Entity { get; set; }

    /// <summary>
    /// Gets or sets the response message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Query to retrieve the schema information of a processor entity.
/// </summary>
public class GetProcessorSchemaQuery
{
    /// <summary>
    /// Gets or sets the unique identifier of the processor.
    /// </summary>
    public Guid ProcessorId { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the schema information.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Response for the GetProcessorSchemaQuery.
/// </summary>
public class GetProcessorSchemaQueryResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the query was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the input schema identifier.
    /// </summary>
    public Guid? InputSchemaId { get; set; }

    /// <summary>
    /// Gets or sets the output schema identifier.
    /// </summary>
    public Guid? OutputSchemaId { get; set; }

    /// <summary>
    /// Gets or sets the response message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
