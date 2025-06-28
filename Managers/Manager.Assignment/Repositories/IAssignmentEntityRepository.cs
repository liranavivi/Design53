
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Entities;
using Shared.Repositories.Interfaces;

namespace Manager.Assignment.Repositories;

public interface IAssignmentEntityRepository : IBaseRepository<AssignmentEntity>
{
    Task<IEnumerable<AssignmentEntity>> GetByVersionAsync(string version);
    Task<IEnumerable<AssignmentEntity>> GetByNameAsync(string name);
    Task<IEnumerable<AssignmentEntity>> GetByStepIdAsync(Guid stepId);
    Task<IEnumerable<AssignmentEntity>> GetByEntityIdAsync(Guid entityId);

    /// <summary>
    /// Check if any assignment entities reference the specified entity ID
    /// </summary>
    /// <param name="entityId">The entity ID to check for references</param>
    /// <returns>True if any assignment entities reference the entity, false otherwise</returns>
    Task<bool> HasEntityReferences(Guid entityId);
}
