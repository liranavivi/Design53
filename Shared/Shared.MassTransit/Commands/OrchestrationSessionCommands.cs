namespace Shared.MassTransit.Commands;

/// <summary>
/// Command to create a new orchestrationsession entity.
/// </summary>
public class CreateOrchestrationSessionCommand
{
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
    /// Gets or sets the user who requested the creation.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to update an existing orchestrationsession entity.
/// </summary>
public class UpdateOrchestrationSessionCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the orchestrationsession to update.
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
    /// Gets or sets the user who requested the update.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to delete a orchestrationsession entity.
/// </summary>
public class DeleteOrchestrationSessionCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the orchestrationsession to delete.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the deletion.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Query to retrieve a orchestrationsession entity.
/// </summary>
public class GetOrchestrationSessionQuery
{
    /// <summary>
    /// Gets or sets the unique identifier of the orchestrationsession to retrieve.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Gets or sets the composite key of the orchestrationsession to retrieve.
    /// </summary>
    public string? CompositeKey { get; set; }
}

/// <summary>
/// Query to retrieve the definition of a orchestrationsession entity.
/// </summary>
public class GetOrchestrationSessionDefinitionQuery
{
    /// <summary>
    /// Gets or sets the unique identifier of the orchestrationsession.
    /// </summary>
    public Guid OrchestrationSessionId { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the definition.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Response for the GetOrchestrationSessionDefinitionQuery.
/// </summary>
public class GetOrchestrationSessionDefinitionQueryResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the query was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the orchestrationsession definition.
    /// </summary>
    public string? Definition { get; set; }

    /// <summary>
    /// Gets or sets the response message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
