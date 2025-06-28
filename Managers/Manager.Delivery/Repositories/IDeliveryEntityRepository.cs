
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Entities;
using Shared.Repositories.Interfaces;

namespace Manager.Delivery.Repositories;

public interface IDeliveryEntityRepository : IBaseRepository<DeliveryEntity>
{
    Task<IEnumerable<DeliveryEntity>> GetByVersionAsync(string version);
    Task<IEnumerable<DeliveryEntity>> GetByNameAsync(string name);
    Task<IEnumerable<DeliveryEntity>> GetByPayloadAsync(string payload);
    Task<IEnumerable<DeliveryEntity>> GetBySchemaIdAsync(Guid schemaId);

    /// <summary>
    /// Check if any delivery entities reference the specified schema ID
    /// </summary>
    /// <param name="schemaId">The schema ID to check for references</param>
    /// <returns>True if any delivery entities reference the schema, false otherwise</returns>
    Task<bool> HasSchemaReferences(Guid schemaId);
}
