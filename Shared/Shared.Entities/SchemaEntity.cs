using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using Shared.Entities.Base;

namespace Shared.Entities;

/// <summary>
/// Represents a schema entity in the system.
/// Contains schema definition information including version, name, and JSON schema definition.
/// </summary>
public class SchemaEntity : BaseEntity
{
    /// <summary>
    /// Gets or sets the JSON schema definition.
    /// This contains the actual schema structure and validation rules.
    /// </summary>
    [BsonElement("definition")]
    [Required(ErrorMessage = "Definition is required")]
    public string Definition { get; set; } = string.Empty;
}
