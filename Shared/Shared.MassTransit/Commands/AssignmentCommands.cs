namespace Shared.MassTransit.Commands;

/// <summary>
/// Command to create a new assignment entity.
/// </summary>
public class CreateAssignmentCommand
{
    /// <summary>
    /// Gets or sets the version of the assignment.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the assignment.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the assignment.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the step identifier of the assignment.
    /// </summary>
    public Guid StepId { get; set; }

    /// <summary>
    /// Gets or sets the entity identifiers of the assignment.
    /// </summary>
    public List<Guid> EntityIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the user who requested the creation.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to update an existing assignment entity.
/// </summary>
public class UpdateAssignmentCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the assignment to update.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the assignment.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the assignment.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the assignment.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the step identifier of the assignment.
    /// </summary>
    public Guid StepId { get; set; }

    /// <summary>
    /// Gets or sets the entity identifiers of the assignment.
    /// </summary>
    public List<Guid> EntityIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the user who requested the update.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to delete a assignment entity.
/// </summary>
public class DeleteAssignmentCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the assignment to delete.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the deletion.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Query to retrieve a assignment entity.
/// </summary>
public class GetAssignmentQuery
{
    /// <summary>
    /// Gets or sets the unique identifier of the assignment to retrieve.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Gets or sets the composite key of the assignment to retrieve.
    /// </summary>
    public string? CompositeKey { get; set; }
}

/// <summary>
/// Query to retrieve the details of a assignment entity.
/// </summary>
public class GetAssignmentDetailsQuery
{
    /// <summary>
    /// Gets or sets the unique identifier of the assignment.
    /// </summary>
    public Guid AssignmentId { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the details.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Response for the GetAssignmentDetailsQuery.
/// </summary>
public class GetAssignmentDetailsQueryResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the query was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the step identifier of the assignment.
    /// </summary>
    public Guid StepId { get; set; }

    /// <summary>
    /// Gets or sets the entity identifiers of the assignment.
    /// </summary>
    public List<Guid> EntityIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the response message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
