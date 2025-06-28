
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Entities;
using Shared.Repositories.Interfaces;

namespace Manager.Processor.Repositories;

public interface IProcessorEntityRepository : IBaseRepository<ProcessorEntity>
{
    Task<IEnumerable<ProcessorEntity>> GetByVersionAsync(string version);
    Task<IEnumerable<ProcessorEntity>> GetByNameAsync(string name);
    Task<IEnumerable<ProcessorEntity>> GetByInputSchemaIdAsync(Guid inputSchemaId);
    Task<IEnumerable<ProcessorEntity>> GetByOutputSchemaIdAsync(Guid outputSchemaId);

    /// <summary>
    /// Check if any processor entities reference the specified schema ID as input schema
    /// </summary>
    /// <param name="schemaId">The schema ID to check for references</param>
    /// <returns>True if any processor entities reference the schema as input schema, false otherwise</returns>
    Task<bool> HasInputSchemaReferences(Guid schemaId);

    /// <summary>
    /// Check if any processor entities reference the specified schema ID as output schema
    /// </summary>
    /// <param name="schemaId">The schema ID to check for references</param>
    /// <returns>True if any processor entities reference the schema as output schema, false otherwise</returns>
    Task<bool> HasOutputSchemaReferences(Guid schemaId);
}
