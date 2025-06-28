using System;
using System.Threading.Tasks;

namespace Manager.Address.Services;

/// <summary>
/// Interface for HTTP communication with other entity managers
/// </summary>
public interface IManagerHttpClient
{
    /// <summary>
    /// Check if an entity is referenced by any assignment entities
    /// </summary>
    /// <param name="entityId">The entity ID to check</param>
    /// <returns>True if entity is referenced, false otherwise</returns>
    Task<bool> CheckEntityReferencesInAssignments(Guid entityId);

    /// <summary>
    /// Check if a schema exists in the Schema Manager
    /// </summary>
    /// <param name="schemaId">The schema ID to check</param>
    /// <returns>True if schema exists, false otherwise</returns>
    Task<bool> CheckSchemaExists(Guid schemaId);
}
