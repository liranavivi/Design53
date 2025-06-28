using Shared.Entities;

namespace Shared.Models;

/// <summary>
/// Model containing all assignment-related data retrieved from Assignment Manager
/// </summary>
public class AssignmentManagerModel
{
    /// <summary>
    /// Dictionary of assignments with stepId as key and list of assignment models as value
    /// Can contain both address and delivery entities
    /// </summary>
    public Dictionary<Guid, List<AssignmentModel>> Assignments { get; set; } = new();
}
