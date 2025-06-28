namespace Shared.MassTransit.Commands;

/// <summary>
/// Command to create a new workflow entity.
/// </summary>
public class CreateWorkflowCommand
{
    /// <summary>
    /// Gets or sets the version of the workflow.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the workflow.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the workflow.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of step IDs that belong to this workflow.
    /// </summary>
    public List<Guid> StepIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the user who requested the creation.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to update an existing workflow entity.
/// </summary>
public class UpdateWorkflowCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the workflow to update.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the workflow.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the workflow.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the workflow.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of step IDs that belong to this workflow.
    /// </summary>
    public List<Guid> StepIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the user who requested the update.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to delete a workflow entity.
/// </summary>
public class DeleteWorkflowCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the workflow to delete.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the deletion.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Query to retrieve a workflow entity.
/// </summary>
public class GetWorkflowQuery
{
    /// <summary>
    /// Gets or sets the unique identifier of the workflow to retrieve.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Gets or sets the composite key of the workflow to retrieve.
    /// </summary>
    public string? CompositeKey { get; set; }
}

/// <summary>
/// Query to retrieve the step IDs of a workflow entity.
/// </summary>
public class GetWorkflowStepsQuery
{
    /// <summary>
    /// Gets or sets the unique identifier of the workflow.
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the step IDs.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Response for the GetWorkflowStepsQuery.
/// </summary>
public class GetWorkflowStepsQueryResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the query was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the workflow step IDs.
    /// </summary>
    public List<Guid>? StepIds { get; set; }

    /// <summary>
    /// Gets or sets the response message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
