using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using Shared.Entities.Base;
using Shared.Entities.Validation;

namespace Shared.Entities;

/// <summary>
/// Represents a processor entity in the system.
/// Contains Processor information including version, name, input schema ID, and output schema ID.
/// </summary>
public class ProcessorEntity : BaseEntity
{
    /// <summary>
    /// Gets or sets the input schema identifier.
    /// This references the schema used for input validation.
    /// </summary>
    [BsonElement("inputSchemaId")]
    [Required(ErrorMessage = "InputSchemaId is required")]
    [NotEmptyGuid(ErrorMessage = "InputSchemaId cannot be empty")]
    public Guid InputSchemaId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets the output schema identifier.
    /// This references the schema used for output validation.
    /// </summary>
    [BsonElement("outputSchemaId")]
    [Required(ErrorMessage = "OutputSchemaId is required")]
    [NotEmptyGuid(ErrorMessage = "OutputSchemaId cannot be empty")]
    public Guid OutputSchemaId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets the SHA-256 hash of the processor implementation.
    /// Used for runtime integrity validation to ensure version consistency.
    /// </summary>
    [BsonElement("implementationHash")]
    public string ImplementationHash { get; set; } = string.Empty;
}
