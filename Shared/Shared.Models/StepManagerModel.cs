using Shared.Entities;

namespace Shared.Models;

/// <summary>
/// Model containing all step-related data retrieved from Step Manager
/// </summary>
public class StepManagerModel
{
    /// <summary>
    /// List of processor IDs from the steps entities
    /// </summary>
    public List<Guid> ProcessorIds { get; set; } = new();

    /// <summary>
    /// List of step IDs
    /// </summary>
    public List<Guid> StepIds { get; set; } = new();

    /// <summary>
    /// List of next step IDs from all steps
    /// </summary>
    public List<Guid> NextStepIds { get; set; } = new();

    /// <summary>
    /// Dictionary of step entities with stepId as key and stepEntity as value
    /// </summary>
    public Dictionary<Guid, StepEntity> StepEntities { get; set; } = new();
}
