using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using Shared.Entities.Base;
using Shared.Entities.Validation;

namespace Shared.Entities;

/// <summary>
/// Represents a workflow entity in the system.
/// Contains Workflow information including version, name, and step references.
/// </summary>
public class WorkflowEntity : BaseEntity
{
    /// <summary>
    /// Gets or sets the list of step IDs that belong to this workflow.
    /// This creates referential integrity relationships with StepEntity records.
    /// </summary>
    [BsonElement("stepIds")]
    [BsonRepresentation(BsonType.String)]
    [Required(ErrorMessage = "StepIds is required")]
    [NotEmptyCollection(ErrorMessage = "StepIds cannot be empty")]
    [NoEmptyGuids(ErrorMessage = "StepIds cannot contain empty GUIDs")]
    public List<Guid> StepIds { get; set; } = new List<Guid>();
}
