using Shared.Entities;
using Shared.Entities.Enums;

namespace Shared.MassTransit.Commands;

/// <summary>
/// Command to create a new step entity.
/// </summary>
public class CreateStepCommand
{
    /// <summary>
    /// Gets or sets the version of the step.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the step.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the step.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the processor identifier that will execute this step.
    /// </summary>
    public Guid ProcessorId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets the collection of next step identifiers.
    /// </summary>
    public List<Guid>? NextStepIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the entry condition for this step.
    /// </summary>
    public StepEntryCondition EntryCondition { get; set; } = StepEntryCondition.PreviousSuccess;

    /// <summary>
    /// Gets or sets the user who requested the creation.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to update an existing step entity.
/// </summary>
public class UpdateStepCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the step to update.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version of the step.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the step.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the step.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the processor identifier that will execute this step.
    /// </summary>
    public Guid ProcessorId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets the collection of next step identifiers.
    /// </summary>
    public List<Guid>? NextStepIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the entry condition for this step.
    /// </summary>
    public StepEntryCondition EntryCondition { get; set; } = StepEntryCondition.PreviousSuccess;

    /// <summary>
    /// Gets or sets the user who requested the update.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Command to delete a step entity.
/// </summary>
public class DeleteStepCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the step to delete.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the deletion.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Query to retrieve a step entity.
/// </summary>
public class GetStepQuery
{
    /// <summary>
    /// Gets or sets the unique identifier of the step to retrieve.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Gets or sets the composite key of the step to retrieve.
    /// </summary>
    public string? CompositeKey { get; set; }
}

/// <summary>
/// Query to retrieve the details of a step entity.
/// </summary>
public class GetStepDetailsQuery
{
    /// <summary>
    /// Gets or sets the unique identifier of the step.
    /// </summary>
    public Guid StepId { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the details.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Response for the GetStepDetailsQuery.
/// </summary>
public class GetStepDetailsQueryResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the query was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the step entity.
    /// </summary>
    public StepEntity? Entity { get; set; }

    /// <summary>
    /// Gets or sets the response message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
