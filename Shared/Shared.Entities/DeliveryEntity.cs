using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using Shared.Entities.Base;
using Shared.Entities.Validation;

namespace Shared.Entities;

/// <summary>
/// Represents a delivery entity in the system.
/// Contains Delivery payload information including version, name, and payload data.
/// </summary>
public class DeliveryEntity : BaseEntity
{
    /// <summary>
    /// Gets or sets the payload data.
    /// This contains the actual delivery payload content.
    /// </summary>
    [BsonElement("payload")]
    [Required(ErrorMessage = "Payload is required")]
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the schema identifier.
    /// This links the delivery entity to a specific schema.
    /// </summary>
    [BsonElement("schemaId")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    [Required(ErrorMessage = "SchemaId is required")]
    [NotEmptyGuid(ErrorMessage = "SchemaId cannot be empty")]
    public Guid SchemaId { get; set; } = Guid.Empty;
}
