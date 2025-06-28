using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using Shared.Entities.Base;
using Shared.Entities.Validation;

namespace Shared.Entities;

/// <summary>
/// Represents a assignment entity in the system.
/// Contains Assignment information including version, name, step ID, and entity IDs.
/// </summary>
public class AssignmentEntity : BaseEntity
{
    /// <summary>
    /// Gets or sets the step identifier.
    /// This references the step that this assignment is associated with.
    /// </summary>
    [BsonElement("stepId")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    [Required(ErrorMessage = "StepId is required")]
    [NotEmptyGuid(ErrorMessage = "StepId cannot be empty")]
    public Guid StepId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets the list of entity identifiers.
    /// This contains the IDs of entities associated with this assignment.
    /// </summary>
    [BsonElement("entityIds")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    [Required(ErrorMessage = "EntityIds is required")]
    [NotEmptyCollection(ErrorMessage = "EntityIds cannot be empty")]
    [NoEmptyGuids(ErrorMessage = "EntityIds cannot contain empty GUIDs")]
    public List<Guid> EntityIds { get; set; } = new List<Guid>();
}
