
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Entities;
using Shared.Repositories.Interfaces;

namespace Manager.Step.Repositories;

public interface IStepEntityRepository : IBaseRepository<StepEntity>
{
    Task<IEnumerable<StepEntity>> GetByVersionAsync(string version);
    Task<IEnumerable<StepEntity>> GetByNameAsync(string name);
    Task<IEnumerable<StepEntity>> GetByProcessorIdAsync(Guid processorId);
    Task<IEnumerable<StepEntity>> GetByNextStepIdAsync(Guid stepId);
    Task<IEnumerable<StepEntity>> GetByEntryConditionAsync(Shared.Entities.Enums.StepEntryCondition entryCondition);
    Task<bool> ExistsAsync(Guid stepId);
}
