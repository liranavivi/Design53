
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Entities;
using Shared.Repositories.Interfaces;

namespace Manager.OrchestratedFlow.Repositories;

public interface IOrchestratedFlowEntityRepository : IBaseRepository<OrchestratedFlowEntity>
{
    Task<IEnumerable<OrchestratedFlowEntity>> GetByVersionAsync(string version);
    Task<IEnumerable<OrchestratedFlowEntity>> GetByNameAsync(string name);
    Task<IEnumerable<OrchestratedFlowEntity>> GetByWorkflowIdAsync(Guid workflowId);
    Task<IEnumerable<OrchestratedFlowEntity>> GetByAssignmentIdAsync(Guid assignmentId);

    /// <summary>
    /// Check if any OrchestratedFlow entities reference the specified workflow ID
    /// </summary>
    /// <param name="workflowId">The workflow ID to check for references</param>
    /// <returns>True if any OrchestratedFlow entities reference the workflow, false otherwise</returns>
    Task<bool> HasWorkflowReferences(Guid workflowId);

    /// <summary>
    /// Check if any OrchestratedFlow entities reference the specified assignment ID
    /// </summary>
    /// <param name="assignmentId">The assignment ID to check for references</param>
    /// <returns>True if any OrchestratedFlow entities reference the assignment, false otherwise</returns>
    Task<bool> HasAssignmentReferences(Guid assignmentId);
}
