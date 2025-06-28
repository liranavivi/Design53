using System;
using System.Threading.Tasks;

namespace Manager.Address.Services;

/// <summary>
/// Interface for validating entity references across all entity managers
/// </summary>
public interface IEntityReferenceValidator
{
    /// <summary>
    /// Check if an entity has any references in assignment entities
    /// </summary>
    /// <param name="entityId">The entity ID to check</param>
    /// <returns>True if entity has references, false otherwise</returns>
    Task<bool> HasAssignmentReferences(Guid entityId);

    /// <summary>
    /// Validate that an entity can be deleted (no references exist)
    /// </summary>
    /// <param name="entityId">The entity ID to validate</param>
    /// <exception cref="InvalidOperationException">Thrown if entity cannot be deleted due to references or validation service unavailable</exception>
    Task ValidateEntityCanBeDeleted(Guid entityId);

    /// <summary>
    /// Validate that an entity can be updated (for future use if needed)
    /// </summary>
    /// <param name="entityId">The entity ID to validate</param>
    /// <exception cref="InvalidOperationException">Thrown if entity cannot be updated due to references or validation service unavailable</exception>
    Task ValidateEntityCanBeUpdated(Guid entityId);
}
