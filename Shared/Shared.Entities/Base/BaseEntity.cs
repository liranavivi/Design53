using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Shared.Entities.Base;

/// <summary>
/// Base entity class that provides common properties and functionality for all entities.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the entity.
    /// MongoDB will auto-generate this value when the entity is created.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } // MongoDB will auto-generate

    /// <summary>
    /// Gets or sets the version of the schema.
    /// This is used to track different versions of the same schema.
    /// Must follow the pattern d.d.d where d is a digit (e.g., 1.0.0, 2.1.3).
    /// </summary>
    [BsonElement("version")]
    [Required(ErrorMessage = "Version is required")]
    [StringLength(50, ErrorMessage = "Version cannot exceed 50 characters")]
    [RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "Version must follow the pattern d.d.d where d is a digit (e.g., 1.0.0, 2.1.3)")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the schema.
    /// This provides a human-readable identifier for the schema.
    /// </summary>
    [BsonElement("name")]
    [Required(ErrorMessage = "Name is required")]
    [StringLength(50, ErrorMessage = "Name cannot exceed 50 characters")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the entity was created.
    /// </summary>
    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the entity was last updated.
    /// </summary>
    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the user who created the entity.
    /// </summary>
    [BsonElement("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user who last updated the entity.
    /// </summary>
    [BsonElement("updatedBy")]
    public string UpdatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the entity.
    /// </summary>
    [BsonElement("description")]
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets the composite key for this schema entity.
    /// The composite key is formed by combining the version and name.
    /// </summary>
    /// <returns>A string in the format "Version_Name" that uniquely identifies this schema.</returns>
    public virtual string GetCompositeKey() => $"{Version}_{Name}";

    /// <summary>
    /// Helper method to check if entity is new (no ID assigned yet).
    /// </summary>
    /// <returns>True if the entity is new (ID is empty), false otherwise.</returns>
    [BsonIgnore]
    public bool IsNew => Id == Guid.Empty;
}
