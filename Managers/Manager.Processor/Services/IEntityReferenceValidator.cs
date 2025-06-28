using System;
using System.Threading.Tasks;
using Shared.Entities;

namespace Manager.Processor.Services;

/// <summary>
/// Interface for validating processor entity references across all entity managers
/// </summary>
public interface IEntityReferenceValidator
{
    /// <summary>
    /// Check if a processor has any references in step entities
    /// </summary>
    /// <param name="processorId">The processor ID to check</param>
    /// <returns>True if processor has references, false otherwise</returns>
    Task<bool> HasStepReferences(Guid processorId);

    /// <summary>
    /// Validate that a processor entity can be safely deleted
    /// </summary>
    /// <param name="processorId">The processor ID to validate</param>
    /// <exception cref="InvalidOperationException">Thrown if processor cannot be deleted due to references</exception>
    Task ValidateProcessorCanBeDeleted(Guid processorId);

    /// <summary>
    /// Validate that a processor entity can be safely updated with critical property changes
    /// </summary>
    /// <param name="processorId">The processor ID to validate</param>
    /// <param name="existingEntity">The existing processor entity</param>
    /// <param name="updatedEntity">The updated processor entity</param>
    /// <exception cref="InvalidOperationException">Thrown if processor cannot be updated due to references</exception>
    Task ValidateProcessorCanBeUpdated(Guid processorId, ProcessorEntity existingEntity, ProcessorEntity updatedEntity);
}
