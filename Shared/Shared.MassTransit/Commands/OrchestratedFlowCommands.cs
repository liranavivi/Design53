namespace Shared.MassTransit.Commands;

/// <summary>
/// Command to create a new orchestratedflow entity.
/// </summary>
public class CreateOrchestratedFlowCommand
{
    /// <summary>
    /// Gets or sets the version of the orchestratedflow.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the orchestratedflow.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the orchestratedflow.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow identifier.
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// Gets or sets the assignment identifiers.
    /// </summary>
    public List<Guid> AssignmentIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the user who requested the creation.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to update an existing orchestratedflow entity.
/// </summary>
public class UpdateOrchestratedFlowCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the orchestratedflow to update.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the orchestratedflow.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the orchestratedflow.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the orchestratedflow.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow identifier.
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// Gets or sets the assignment identifiers.
    /// </summary>
    public List<Guid> AssignmentIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the user who requested the update.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to delete a orchestratedflow entity.
/// </summary>
public class DeleteOrchestratedFlowCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the orchestratedflow to delete.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the deletion.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Query to retrieve a orchestratedflow entity.
/// </summary>
public class GetOrchestratedFlowQuery
{
    /// <summary>
    /// Gets or sets the unique identifier of the orchestratedflow to retrieve.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Gets or sets the composite key of the orchestratedflow to retrieve.
    /// </summary>
    public string? CompositeKey { get; set; }
}


