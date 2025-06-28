using Shared.Correlation;

namespace Manager.Step.Services;

/// <summary>
/// Service for validating processor references during step entity operations
/// Implements strong consistency model with fail-safe approach
/// </summary>
public class ProcessorValidationService : IProcessorValidationService
{
    private readonly IManagerHttpClient _httpClient;
    private readonly ILogger<ProcessorValidationService> _logger;
    private readonly bool _enableProcessorValidation;

    public ProcessorValidationService(
        IManagerHttpClient httpClient,
        IConfiguration configuration,
        ILogger<ProcessorValidationService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Read configuration settings
        _enableProcessorValidation = configuration.GetValue<bool>("ReferentialIntegrity:ValidateProcessorReferences", true);
    }

    public async Task ValidateProcessorExists(Guid processorId)
    {
        if (!_enableProcessorValidation)
        {
            _logger.LogDebugWithCorrelation("Processor validation is disabled. Skipping validation for ProcessorId: {ProcessorId}", processorId);
            return;
        }

        if (processorId == Guid.Empty)
        {
            var message = "ProcessorId cannot be empty";
            _logger.LogWarningWithCorrelation("Processor validation failed: {Message}", message);
            throw new InvalidOperationException(message);
        }

        _logger.LogDebugWithCorrelation("Validating processor exists. ProcessorId: {ProcessorId}", processorId);

        try
        {
            var processorExists = await _httpClient.CheckProcessorExists(processorId);
            
            if (!processorExists)
            {
                var message = $"Processor with ID {processorId} does not exist";
                _logger.LogWarningWithCorrelation("Processor validation failed: {Message}", message);
                throw new InvalidOperationException(message);
            }
            
            _logger.LogDebugWithCorrelation("Processor validation passed. ProcessorId: {ProcessorId}", processorId);
        }
        catch (InvalidOperationException)
        {
            // Re-throw validation exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Processor validation failed due to unexpected error. ProcessorId: {ProcessorId}", processorId);
            
            // Fail-safe approach: if validation service is unavailable, reject the operation
            throw new InvalidOperationException($"Processor validation service is unavailable. Operation rejected for data integrity.", ex);
        }
    }

    public async Task ValidateNextStepsExist(List<Guid> nextStepIds)
    {
        if (!_enableProcessorValidation)
        {
            _logger.LogDebugWithCorrelation("Next step validation is disabled. Skipping validation for NextStepIds: {NextStepIds}",
                string.Join(",", nextStepIds));
            return;
        }

        if (nextStepIds == null || nextStepIds.Count == 0)
        {
            _logger.LogDebugWithCorrelation("NextStepIds is null or empty. Skipping validation.");
            return;
        }

        // Check for empty GUIDs (this should be caught by model validation, but double-check)
        var emptyGuids = nextStepIds.Where(id => id == Guid.Empty).ToList();
        if (emptyGuids.Any())
        {
            var message = "NextStepIds cannot contain empty GUIDs";
            _logger.LogWarningWithCorrelation("Next step validation failed: {Message}", message);
            throw new InvalidOperationException(message);
        }

        _logger.LogDebugWithCorrelation("Validating next steps exist. NextStepIds: {NextStepIds}", string.Join(",", nextStepIds));

        var validationTasks = nextStepIds.Select(async stepId =>
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
                _logger.LogWarningWithCorrelation("Next step validation failed: {Message}", message);
                throw new InvalidOperationException(message);
            }

            _logger.LogDebugWithCorrelation("Next step validation passed. All NextStepIds exist: {NextStepIds}", string.Join(",", nextStepIds));
        }
        catch (InvalidOperationException)
        {
            // Re-throw validation exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Next step validation failed due to unexpected error. NextStepIds: {NextStepIds}",
                string.Join(",", nextStepIds));

            // Fail-safe approach: if validation service is unavailable, reject the operation
            throw new InvalidOperationException($"Next step validation service is unavailable. Operation rejected for data integrity.", ex);
        }
    }
}
