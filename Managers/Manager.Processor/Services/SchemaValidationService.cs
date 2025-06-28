using Shared.Correlation;

namespace Manager.Processor.Services;

/// <summary>
/// Service for validating dual schema references during processor entity operations
/// Implements strong consistency model with fail-safe approach
/// </summary>
public class SchemaValidationService : ISchemaValidationService
{
    private readonly IManagerHttpClient _httpClient;
    private readonly ILogger<SchemaValidationService> _logger;
    private readonly bool _enableSchemaValidation;

    public SchemaValidationService(
        IManagerHttpClient httpClient,
        IConfiguration configuration,
        ILogger<SchemaValidationService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Read configuration settings
        _enableSchemaValidation = configuration.GetValue<bool>("ReferentialIntegrity:ValidateSchemaReferences", true);
    }

    public async Task ValidateSchemasExist(Guid inputSchemaId, Guid outputSchemaId)
    {
        if (!_enableSchemaValidation)
        {
            _logger.LogDebugWithCorrelation("Schema validation is disabled. Skipping validation for InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}", 
                inputSchemaId, outputSchemaId);
            return;
        }

        // Validate input parameters
        if (inputSchemaId == Guid.Empty)
        {
            var message = "InputSchemaId cannot be empty";
            _logger.LogWarningWithCorrelation("Schema validation failed: {Message}", message);
            throw new InvalidOperationException(message);
        }

        if (outputSchemaId == Guid.Empty)
        {
            var message = "OutputSchemaId cannot be empty";
            _logger.LogWarningWithCorrelation("Schema validation failed: {Message}", message);
            throw new InvalidOperationException(message);
        }

        _logger.LogDebugWithCorrelation("Validating schemas exist. InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}", 
            inputSchemaId, outputSchemaId);

        try
        {
            // Validate both schemas in parallel for better performance
            var inputSchemaTask = _httpClient.CheckSchemaExists(inputSchemaId);
            var outputSchemaTask = _httpClient.CheckSchemaExists(outputSchemaId);

            await Task.WhenAll(inputSchemaTask, outputSchemaTask);

            var inputSchemaExists = await inputSchemaTask;
            var outputSchemaExists = await outputSchemaTask;

            // Check input schema
            if (!inputSchemaExists)
            {
                var message = $"Input schema with ID {inputSchemaId} does not exist";
                _logger.LogWarningWithCorrelation("Schema validation failed: {Message}", message);
                throw new InvalidOperationException(message);
            }

            // Check output schema
            if (!outputSchemaExists)
            {
                var message = $"Output schema with ID {outputSchemaId} does not exist";
                _logger.LogWarningWithCorrelation("Schema validation failed: {Message}", message);
                throw new InvalidOperationException(message);
            }
            
            _logger.LogDebugWithCorrelation("Schema validation passed. InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}", 
                inputSchemaId, outputSchemaId);
        }
        catch (InvalidOperationException)
        {
            // Re-throw validation exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Schema validation failed due to unexpected error. InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}", 
                inputSchemaId, outputSchemaId);
            
            // Fail-safe approach: if validation service is unavailable, reject the operation
            throw new InvalidOperationException($"Schema validation service is unavailable. Operation rejected for data integrity.", ex);
        }
    }
}
