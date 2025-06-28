using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.ComponentModel.DataAnnotations;
using Shared.Entities.Base;
using Shared.Entities.Validation;
using Shared.Entities.Enums;

namespace Shared.Entities;

/// <summary>
/// Represents a step entity in the system.
/// Contains Step information including version, name, processor reference, and workflow navigation.
/// </summary>
public class StepEntity : BaseEntity
{
    /// <summary>
    /// Gets or sets the processor identifier.
    /// This references the ProcessorEntity that will execute this step.
    /// </summary>
    [BsonElement("processorId")]
    [BsonRepresentation(BsonType.String)]
    [Required(ErrorMessage = "ProcessorId is required")]
    [NotEmptyGuid(ErrorMessage = "ProcessorId cannot be empty")]
    public Guid ProcessorId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets the collection of next step identifiers.
    /// This defines the possible next steps in the workflow after this step completes.
    /// Can be empty for terminal steps.
    /// </summary>
    [BsonElement("nextStepIds")]
    [BsonRepresentation(BsonType.String)]
    [NoEmptyGuids(ErrorMessage = "NextStepIds cannot contain empty GUIDs")]
    public List<Guid> NextStepIds { get; set; } = new List<Guid>();

    /// <summary>
    /// Gets or sets the entry condition that determines when this step should execute.
    /// Controls the workflow execution logic and step transitions.
    /// </summary>
    [BsonElement("entryCondition")]
    [BsonRepresentation(BsonType.String)]
    [Required(ErrorMessage = "EntryCondition is required")]
    public StepEntryCondition EntryCondition { get; set; } = StepEntryCondition.PreviousSuccess;
}
