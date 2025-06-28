using Shared.Correlation;

namespace Manager.Step.Services;

/// <summary>
/// Service for validating entity references before delete/update operations
/// </summary>
public class EntityReferenceValidator : IEntityReferenceValidator
{
    private readonly IManagerHttpClient _httpClient;
    private readonly ILogger<EntityReferenceValidator> _logger;
    private readonly bool _enableParallelValidation;
    private readonly bool _validateAssignmentReferences;
    private readonly bool _validateWorkflowReferences;

    public EntityReferenceValidator(
        IManagerHttpClient httpClient,
        IConfiguration configuration,
        ILogger<EntityReferenceValidator> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Read configuration settings
        _enableParallelValidation = configuration.GetValue<bool>("ReferentialIntegrity:EnableParallelValidation", true);
        _validateAssignmentReferences = configuration.GetValue<bool>("ReferentialIntegrity:ValidateAssignmentReferences", true);
        _validateWorkflowReferences = configuration.GetValue<bool>("ReferentialIntegrity:ValidateWorkflowReferences", true);
    }

    public async Task ValidateStepCanBeDeleted(Guid stepId)
    {
        _logger.LogDebugWithCorrelation("Validating step can be deleted. StepId: {StepId}", stepId);

        try
        {
            var hasReferences = await HasReferences(stepId);
            
            if (hasReferences)
            {
                var message = $"Cannot delete Step entity {stepId}: it is referenced by one or more Assignment or Workflow entities";
                _logger.LogWarningWithCorrelation("Step deletion blocked due to references. StepId: {StepId}, Message: {Message}", 
                    stepId, message);
                
                throw new InvalidOperationException(message);
            }
            
            _logger.LogDebugWithCorrelation("Step validation passed - no references found. StepId: {StepId}", stepId);
        }
        catch (InvalidOperationException)
        {
            // Re-throw validation exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error during step deletion validation. StepId: {StepId}", stepId);
            
            // Fail-safe: If validation fails, prevent deletion
            throw new InvalidOperationException($"Step validation failed: {ex.Message}");
        }
    }

    public async Task ValidateStepCanBeUpdated(Guid stepId)
    {
        _logger.LogDebugWithCorrelation("Validating step can be updated. StepId: {StepId}", stepId);

        try
        {
            var hasReferences = await HasReferences(stepId);
            
            if (hasReferences)
            {
                var message = $"Cannot update Step entity {stepId}: it is referenced by one or more Assignment or Workflow entities";
                _logger.LogWarningWithCorrelation("Step update blocked due to references. StepId: {StepId}, Message: {Message}", 
                    stepId, message);
                
                throw new InvalidOperationException(message);
            }
            
            _logger.LogDebugWithCorrelation("Step validation passed - no references found. StepId: {StepId}", stepId);
        }
        catch (InvalidOperationException)
        {
            // Re-throw validation exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error during step update validation. StepId: {StepId}", stepId);
            
            // Fail-safe: If validation fails, prevent update
            throw new InvalidOperationException($"Step validation failed: {ex.Message}");
        }
    }

    private async Task<bool> HasReferences(Guid stepId)
    {
        var validationTasks = new List<Task<bool>>();

        // Add assignment reference check if enabled
        if (_validateAssignmentReferences)
        {
            validationTasks.Add(_httpClient.CheckAssignmentStepReferences(stepId));
        }

        // Add workflow reference check if enabled
        if (_validateWorkflowReferences)
        {
            validationTasks.Add(_httpClient.CheckWorkflowStepReferences(stepId));
        }

        if (!validationTasks.Any())
        {
            _logger.LogWarningWithCorrelation("No reference validation tasks configured. StepId: {StepId}", stepId);
            return false;
        }

        try
        {
            bool[] results;
            
            if (_enableParallelValidation)
            {
                _logger.LogDebugWithCorrelation("Executing {TaskCount} validation tasks in parallel. StepId: {StepId}", 
                    validationTasks.Count, stepId);
                
                results = await Task.WhenAll(validationTasks);
            }
            else
            {
                _logger.LogDebugWithCorrelation("Executing {TaskCount} validation tasks sequentially. StepId: {StepId}", 
                    validationTasks.Count, stepId);
                
                results = new bool[validationTasks.Count];
                for (int i = 0; i < validationTasks.Count; i++)
                {
                    results[i] = await validationTasks[i];
                }
            }

            var hasReferences = results.Any(hasRef => hasRef);
            
            _logger.LogDebugWithCorrelation("Reference validation completed. StepId: {StepId}, HasReferences: {HasReferences}, Results: [{Results}]",
                stepId, hasReferences, string.Join(", ", results));

            return hasReferences;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error during reference validation. StepId: {StepId}", stepId);
            throw;
        }
    }
}
