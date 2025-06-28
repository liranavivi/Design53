using Shared.Correlation;
using Shared.Entities;

namespace Manager.Processor.Services;

/// <summary>
/// Service for validating processor entity references with fail-safe behavior
/// </summary>
public class EntityReferenceValidator : IEntityReferenceValidator
{
    private readonly IManagerHttpClient _managerHttpClient;
    private readonly ILogger<EntityReferenceValidator> _logger;

    public EntityReferenceValidator(
        IManagerHttpClient managerHttpClient,
        ILogger<EntityReferenceValidator> logger)
    {
        _managerHttpClient = managerHttpClient;
        _logger = logger;
    }

    public async Task<bool> HasStepReferences(Guid processorId)
    {
        _logger.LogDebugWithCorrelation("Checking step references for ProcessorId: {ProcessorId}", processorId);

        try
        {
            var hasReferences = await _managerHttpClient.CheckProcessorReferencesInSteps(processorId);
            
            _logger.LogDebugWithCorrelation("Step reference check completed for ProcessorId: {ProcessorId}. HasReferences: {HasReferences}", 
                processorId, hasReferences);
            
            return hasReferences;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Step reference check failed for ProcessorId: {ProcessorId}", processorId);
            
            // Re-throw the exception to maintain fail-safe behavior
            throw;
        }
    }

    public async Task ValidateProcessorCanBeDeleted(Guid processorId)
    {
        _logger.LogDebugWithCorrelation("Validating processor can be deleted. ProcessorId: {ProcessorId}", processorId);

        try
        {
            var hasReferences = await HasStepReferences(processorId);
            
            if (hasReferences)
            {
                var message = $"Cannot delete Processor entity {processorId}: it is referenced by one or more Step entities";
                _logger.LogWarningWithCorrelation("Processor deletion blocked due to references. ProcessorId: {ProcessorId}, Message: {Message}", 
                    processorId, message);
                
                throw new InvalidOperationException(message);
            }
            
            _logger.LogDebugWithCorrelation("Processor validation passed - no references found. ProcessorId: {ProcessorId}", processorId);
        }
        catch (InvalidOperationException)
        {
            // Re-throw validation exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Processor deletion validation failed. ProcessorId: {ProcessorId}", processorId);
            
            // Wrap other exceptions to maintain fail-safe behavior
            throw new InvalidOperationException($"Processor reference validation failed. Operation rejected for safety.", ex);
        }
    }

    public async Task ValidateProcessorCanBeUpdated(Guid processorId, ProcessorEntity existingEntity, ProcessorEntity updatedEntity)
    {
        _logger.LogDebugWithCorrelation("Validating processor can be updated. ProcessorId: {ProcessorId}", processorId);

        try
        {
            // Check if critical properties are being changed
            bool hasCriticalChanges = HasCriticalPropertyChanges(existingEntity, updatedEntity);
            
            if (!hasCriticalChanges)
            {
                _logger.LogDebugWithCorrelation("No critical property changes detected - update allowed. ProcessorId: {ProcessorId}", processorId);
                return;
            }

            _logger.LogDebugWithCorrelation("Critical property changes detected - checking references. ProcessorId: {ProcessorId}", processorId);
            
            var hasReferences = await HasStepReferences(processorId);
            
            if (hasReferences)
            {
                var message = $"Cannot update Processor entity {processorId}: critical properties cannot be changed while referenced by Step entities";
                _logger.LogWarningWithCorrelation("Processor update blocked due to references. ProcessorId: {ProcessorId}, Message: {Message}", 
                    processorId, message);
                
                throw new InvalidOperationException(message);
            }
            
            _logger.LogDebugWithCorrelation("Processor update validation passed - no references found. ProcessorId: {ProcessorId}", processorId);
        }
        catch (InvalidOperationException)
        {
            // Re-throw validation exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Processor update validation failed. ProcessorId: {ProcessorId}", processorId);
            
            // Wrap other exceptions to maintain fail-safe behavior
            throw new InvalidOperationException($"Processor reference validation failed. Operation rejected for safety.", ex);
        }
    }

    private bool HasCriticalPropertyChanges(ProcessorEntity existingEntity, ProcessorEntity updatedEntity)
    {
        // Critical properties that affect step execution
        return existingEntity.InputSchemaId != updatedEntity.InputSchemaId ||
               existingEntity.OutputSchemaId != updatedEntity.OutputSchemaId;
    }
}
