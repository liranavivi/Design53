using System;
using System.Threading.Tasks;

namespace Manager.Processor.Services;

/// <summary>
/// Interface for HTTP client communication with other entity managers
/// </summary>
public interface IManagerHttpClient
{
    /// <summary>
    /// Check if a processor is referenced by any step entities
    /// </summary>
    /// <param name="processorId">The processor ID to check</param>
    /// <returns>True if processor has references in step entities, false otherwise</returns>
    Task<bool> CheckProcessorReferencesInSteps(Guid processorId);

    /// <summary>
    /// Check if a schema exists in the Schema Manager
    /// </summary>
    /// <param name="schemaId">The schema ID to check</param>
    /// <returns>True if schema exists, false otherwise</returns>
    Task<bool> CheckSchemaExists(Guid schemaId);
}
