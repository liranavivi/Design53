using Shared.Correlation;

namespace Manager.Assignment.Services;

/// <summary>
/// Service for validating Assignment entity references
/// </summary>
public class AssignmentValidationService : IAssignmentValidationService
{
    private readonly IManagerHttpClient _httpClient;
    private readonly ILogger<AssignmentValidationService> _logger;
    private readonly bool _enableStepValidation;
    private readonly bool _enableEntityValidation;

    public AssignmentValidationService(
        IManagerHttpClient httpClient,
        IConfiguration configuration,
        ILogger<AssignmentValidationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Allow validation to be disabled via configuration for testing/development
        _enableStepValidation = configuration.GetValue<bool>("Validation:EnableStepValidation", true);
        _enableEntityValidation = configuration.GetValue<bool>("Validation:EnableEntityValidation", true);
        
        _logger.LogInformationWithCorrelation("AssignmentValidationService initialized. StepValidation: {StepValidation}, EntityValidation: {EntityValidation}",
            _enableStepValidation, _enableEntityValidation);
    }

    public async Task ValidateStepExists(Guid stepId)
    {
        if (!_enableStepValidation)
        {
            _logger.LogDebugWithCorrelation("Step validation is disabled. Skipping validation for StepId: {StepId}", stepId);
            return;
        }

        // Check for empty GUID (this should be caught by model validation, but double-check)
        if (stepId == Guid.Empty)
        {
            var message = "StepId cannot be empty";
            _logger.LogWarningWithCorrelation("Step validation failed: {Message}", message);
            throw new InvalidOperationException(message);
        }

        _logger.LogDebugWithCorrelation("Validating step exists. StepId: {StepId}", stepId);

        try
        {
            var exists = await _httpClient.CheckStepExists(stepId);
            
            if (!exists)
            {
                var message = $"Step with ID {stepId} does not exist";
                _logger.LogWarningWithCorrelation("Step validation failed: {Message}", message);
                throw new InvalidOperationException(message);
            }

            _logger.LogDebugWithCorrelation("Step validation passed. StepId: {StepId}", stepId);
        }
        catch (InvalidOperationException)
        {
            // Re-throw validation exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Step validation failed due to unexpected error. StepId: {StepId}", stepId);
            
            // Fail-safe approach: if validation service is unavailable, reject the operation
            throw new InvalidOperationException($"Step validation service is unavailable. Operation rejected for data integrity.", ex);
        }
    }

    public async Task ValidateEntitiesExist(List<Guid> entityIds)
    {
        if (!_enableEntityValidation)
        {
            _logger.LogDebugWithCorrelation("Entity validation is disabled. Skipping validation for EntityIds: {EntityIds}", 
                string.Join(",", entityIds));
            return;
        }

        if (entityIds == null || entityIds.Count == 0)
        {
            _logger.LogDebugWithCorrelation("EntityIds is null or empty. Skipping validation.");
            return;
        }

        // Check for empty GUIDs (this should be caught by model validation, but double-check)
        var emptyGuids = entityIds.Where(id => id == Guid.Empty).ToList();
        if (emptyGuids.Any())
        {
            var message = "EntityIds cannot contain empty GUIDs";
            _logger.LogWarningWithCorrelation("Entity validation failed: {Message}", message);
            throw new InvalidOperationException(message);
        }

        _logger.LogDebugWithCorrelation("Validating entities exist. EntityIds: {EntityIds}", string.Join(",", entityIds));

        var validationTasks = entityIds.Select(async entityId =>
        {
            try
            {
                var exists = await _httpClient.CheckEntityExists(entityId);
                return new { EntityId = entityId, Exists = exists };
            }
            catch (Exception ex)
            {
                _logger.LogErrorWithCorrelation(ex, "Failed to validate entity existence. EntityId: {EntityId}", entityId);
                throw new InvalidOperationException($"Entity validation service unavailable for EntityId {entityId}. Operation rejected for data integrity.", ex);
            }
        });

        try
        {
            var results = await Task.WhenAll(validationTasks);
            var nonExistentEntities = results.Where(r => !r.Exists).Select(r => r.EntityId).ToList();

            if (nonExistentEntities.Any())
            {
                var message = $"The following entity IDs do not exist: {string.Join(", ", nonExistentEntities)}";
                _logger.LogWarningWithCorrelation("Entity validation failed: {Message}", message);
                throw new InvalidOperationException(message);
            }

            _logger.LogDebugWithCorrelation("Entity validation passed. All EntityIds exist: {EntityIds}", string.Join(",", entityIds));
        }
        catch (InvalidOperationException)
        {
            // Re-throw validation exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Entity validation failed due to unexpected error. EntityIds: {EntityIds}", 
                string.Join(",", entityIds));
            
            // Fail-safe approach: if validation service is unavailable, reject the operation
            throw new InvalidOperationException($"Entity validation service is unavailable. Operation rejected for data integrity.", ex);
        }
    }
}
