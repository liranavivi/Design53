
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Entities;
using Shared.Repositories.Interfaces;

namespace Manager.Schema.Repositories;

public interface ISchemaEntityRepository : IBaseRepository<SchemaEntity>
{
    Task<IEnumerable<SchemaEntity>> GetByVersionAsync(string version);
    Task<IEnumerable<SchemaEntity>> GetByNameAsync(string name);
    Task<IEnumerable<SchemaEntity>> GetByDefinitionAsync(string definition);
}
