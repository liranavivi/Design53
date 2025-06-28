
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Entities;
using Shared.Repositories.Interfaces;

namespace Manager.Workflow.Repositories;

public interface IWorkflowEntityRepository : IBaseRepository<WorkflowEntity>
{
    Task<IEnumerable<WorkflowEntity>> GetByVersionAsync(string version);
    Task<IEnumerable<WorkflowEntity>> GetByNameAsync(string name);
    Task<IEnumerable<WorkflowEntity>> GetByStepIdAsync(Guid stepId);
    Task<IEnumerable<WorkflowEntity>> GetByStepIdsAsync(List<Guid> stepIds);
}
