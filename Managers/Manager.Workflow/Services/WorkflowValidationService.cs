using Shared.Correlation;

namespace Manager.Workflow.Services;

/// <summary>
/// Service for validating Workflow entity references
/// </summary>
public class WorkflowValidationService : IWorkflowValidationService
{
    private readonly IManagerHttpClient _httpClient;
    private readonly ILogger<WorkflowValidationService> _logger;
    private readonly bool _enableStepValidation;

    public WorkflowValidationService(
        IManagerHttpClient httpClient,
        IConfiguration configuration,
        ILogger<WorkflowValidationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Allow validation to be disabled via configuration for testing/development
        _enableStepValidation = configuration.GetValue<bool>("Validation:EnableStepValidation", true);
        
        _logger.LogInformationWithCorrelation("WorkflowValidationService initialized. StepValidation: {StepValidation}",
            _enableStepValidation);
    }

    public async Task ValidateStepsExist(List<Guid> stepIds)
    {
        if (!_enableStepValidation)
        {
            _logger.LogDebugWithCorrelation("Step validation is disabled. Skipping validation for StepIds: {StepIds}", 
                string.Join(",", stepIds));
            return;
        }

        if (stepIds == null || stepIds.Count == 0)
        {
            _logger.LogDebugWithCorrelation("StepIds is null or empty. Skipping validation.");
            return;
        }

        // Check for empty GUIDs (this should be caught by model validation, but double-check)
        var emptyGuids = stepIds.Where(id => id == Guid.Empty).ToList();
        if (emptyGuids.Any())
        {
            var message = "StepIds cannot contain empty GUIDs";
            _logger.LogWarningWithCorrelation("Step validation failed: {Message}", message);
            throw new InvalidOperationException(message);
        }

        _logger.LogDebugWithCorrelation("Validating steps exist. StepIds: {StepIds}", string.Join(",", stepIds));

        var validationTasks = stepIds.Select(async stepId =>
        {
            try
            {
                var exists = await _httpClient.CheckStepExists(stepId);
                return new { StepId = stepId, Exists = exists };
            }
            catch (Exception ex)
            {
                _logger.LogErrorWithCorrelation(ex, "Failed to validate step existence. StepId: {StepId}", stepId);
                throw new InvalidOperationException($"Step validation service unavailable for StepId {stepId}. Operation rejected for data integrity.", ex);
            }
        });

        try
        {
            var results = await Task.WhenAll(validationTasks);
            var nonExistentSteps = results.Where(r => !r.Exists).Select(r => r.StepId).ToList();

            if (nonExistentSteps.Any())
            {
                var message = $"The following step IDs do not exist: {string.Join(", ", nonExistentSteps)}";
                _logger.LogWarningWithCorrelation("Step validation failed: {Message}", message);
                throw new InvalidOperationException(message);
            }

            _logger.LogDebugWithCorrelation("Step validation passed. All StepIds exist: {StepIds}", string.Join(",", stepIds));
        }
        catch (InvalidOperationException)
        {
            // Re-throw validation exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Step validation failed due to unexpected error. StepIds: {StepIds}", 
                string.Join(",", stepIds));
            
            // Fail-safe approach: if validation service is unavailable, reject the operation
            throw new InvalidOperationException($"Step validation service is unavailable. Operation rejected for data integrity.", ex);
        }
    }
}
