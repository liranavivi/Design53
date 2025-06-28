using System;
using System.Threading.Tasks;

namespace Manager.Schema.Services;

/// <summary>
/// Interface for validating schema references across all entity managers
/// </summary>
public interface ISchemaReferenceValidator
{
    /// <summary>
    /// Check if a schema has any references in other entities
    /// </summary>
    /// <param name="schemaId">The schema ID to check</param>
    /// <returns>True if schema has references, false otherwise</returns>
    Task<bool> HasReferences(Guid schemaId);

    /// <summary>
    /// Get detailed information about schema references
    /// </summary>
    /// <param name="schemaId">The schema ID to check</param>
    /// <returns>Schema reference details</returns>
    Task<SchemaReferenceDetails> GetReferenceDetails(Guid schemaId);
}

/// <summary>
/// Details about schema references across different entity types
/// </summary>
public class SchemaReferenceDetails
{
    public Guid SchemaId { get; set; }
    public bool HasAddressReferences { get; set; }
    public bool HasDeliveryReferences { get; set; }
    public bool HasProcessorInputReferences { get; set; }
    public bool HasProcessorOutputReferences { get; set; }
    public bool HasAnyReferences => HasAddressReferences || HasDeliveryReferences || 
                                   HasProcessorInputReferences || HasProcessorOutputReferences;
    public DateTime CheckedAt { get; set; }
    public string[] FailedChecks { get; set; } = Array.Empty<string>();
}
