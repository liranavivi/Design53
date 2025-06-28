
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Entities;
using Shared.Repositories.Interfaces;

namespace Manager.Address.Repositories;

public interface IAddressEntityRepository : IBaseRepository<AddressEntity>
{
    Task<IEnumerable<AddressEntity>> GetByVersionAsync(string version);
    Task<IEnumerable<AddressEntity>> GetByNameAsync(string name);
    Task<IEnumerable<AddressEntity>> GetByConnectionStringAsync(string connectionString);
    Task<IEnumerable<AddressEntity>> GetBySchemaIdAsync(Guid schemaId);

    /// <summary>
    /// Check if any address entities reference the specified schema ID
    /// </summary>
    /// <param name="schemaId">The schema ID to check for references</param>
    /// <returns>True if any address entities reference the schema, false otherwise</returns>
    Task<bool> HasSchemaReferences(Guid schemaId);
}
