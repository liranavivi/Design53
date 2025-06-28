using Shared.Models;
using Shared.Entities;
namespace Manager.Orchestrator.Models;

/// <summary>
/// Complete orchestration data model for caching
/// </summary>
public class OrchestrationCacheModel
{
    /// <summary>
    /// The orchestrated flow ID this cache entry is for
    /// </summary>
    public Guid OrchestratedFlowId { get; set; }

    /// <summary>
    /// The orchestrated flow entity for this cache entry
    /// </summary>
    public OrchestratedFlowEntity OrchestratedFlow { get; set; } = new();

    /// <summary>
    /// Step manager data containing processor IDs, step IDs, next step IDs, and step entities
    /// </summary>
    public StepManagerModel StepManager { get; set; } = new();

    /// <summary>
    /// Assignment manager data containing assignments by step ID
    /// </summary>
    public AssignmentManagerModel AssignmentManager { get; set; } = new();

    /// <summary>
    /// List of entry point step IDs for this orchestrated flow.
    /// These are the steps that should be executed when starting the workflow.
    /// Calculated once during orchestration setup and cached for reuse.
    /// </summary>
    public List<Guid> EntryPoints { get; set; } = new();

    /// <summary>
    /// Timestamp when this cache entry was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when this cache entry expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Version of the cache model for future compatibility
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Indicates if this cache entry has expired
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}
