using System;
using System.Threading.Tasks;

namespace Manager.Schema.Services;

/// <summary>
/// Interface for HTTP communication with other entity managers
/// </summary>
public interface IManagerHttpClient
{
    /// <summary>
    /// Check if a schema is referenced by any address entities
    /// </summary>
    /// <param name="schemaId">The schema ID to check</param>
    /// <returns>True if schema is referenced, false otherwise</returns>
    Task<bool> CheckAddressSchemaReferences(Guid schemaId);

    /// <summary>
    /// Check if a schema is referenced by any delivery entities
    /// </summary>
    /// <param name="schemaId">The schema ID to check</param>
    /// <returns>True if schema is referenced, false otherwise</returns>
    Task<bool> CheckDeliverySchemaReferences(Guid schemaId);

    /// <summary>
    /// Check if a schema is referenced as input schema by any processor entities
    /// </summary>
    /// <param name="schemaId">The schema ID to check</param>
    /// <returns>True if schema is referenced as input schema, false otherwise</returns>
    Task<bool> CheckProcessorInputSchemaReferences(Guid schemaId);

    /// <summary>
    /// Check if a schema is referenced as output schema by any processor entities
    /// </summary>
    /// <param name="schemaId">The schema ID to check</param>
    /// <returns>True if schema is referenced as output schema, false otherwise</returns>
    Task<bool> CheckProcessorOutputSchemaReferences(Guid schemaId);
}
