using Shared.Correlation;

namespace Manager.Delivery.Services;

/// <summary>
/// Service for validating entity references across all entity managers
/// </summary>
public class EntityReferenceValidator : IEntityReferenceValidator
{
    private readonly IManagerHttpClient _managerHttpClient;
    private readonly ILogger<EntityReferenceValidator> _logger;

    public EntityReferenceValidator(IManagerHttpClient managerHttpClient, ILogger<EntityReferenceValidator> logger)
    {
        _managerHttpClient = managerHttpClient ?? throw new ArgumentNullException(nameof(managerHttpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> HasAssignmentReferences(Guid entityId)
    {
        _logger.LogDebugWithCorrelation("Checking assignment references for EntityId: {EntityId}", entityId);

        try
        {
            var hasReferences = await _managerHttpClient.CheckEntityReferencesInAssignments(entityId);
            
            _logger.LogDebugWithCorrelation("Assignment reference check completed for EntityId: {EntityId}. HasReferences: {HasReferences}", 
                entityId, hasReferences);
            
            return hasReferences;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Assignment reference check failed for EntityId: {EntityId}", entityId);
            
            // Re-throw the exception to maintain fail-safe behavior
            throw;
        }
    }

    public async Task ValidateEntityCanBeDeleted(Guid entityId)
    {
        _logger.LogDebugWithCorrelation("Validating entity can be deleted. EntityId: {EntityId}", entityId);

        try
        {
            var hasReferences = await HasAssignmentReferences(entityId);
            
            if (hasReferences)
            {
                var message = $"Cannot delete Delivery entity {entityId}: it is referenced by one or more Assignment entities";
                _logger.LogWarningWithCorrelation("Entity deletion blocked due to references. EntityId: {EntityId}, Message: {Message}", 
                    entityId, message);
                
                throw new InvalidOperationException(message);
            }
            
            _logger.LogDebugWithCorrelation("Entity validation passed - no references found. EntityId: {EntityId}", entityId);
        }
        catch (InvalidOperationException)
        {
            // Re-throw validation exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Cannot validate Delivery entity {entityId} for deletion: validation service unavailable";
            _logger.LogErrorWithCorrelation(ex, "Entity validation failed due to service unavailability. EntityId: {EntityId}, Message: {Message}", 
                entityId, message);
            
            // Fail-safe: if we can't validate, prevent the operation
            throw new InvalidOperationException(message, ex);
        }
    }

    public async Task ValidateEntityCanBeUpdated(Guid entityId)
    {
        _logger.LogDebugWithCorrelation("Validating entity can be updated. EntityId: {EntityId}", entityId);

        // For now, we don't block updates based on references
        // This method is provided for future extensibility
        // If needed, similar validation logic can be implemented here
        
        _logger.LogDebugWithCorrelation("Entity update validation passed. EntityId: {EntityId}", entityId);
        
        await Task.CompletedTask;
    }
}
