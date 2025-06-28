using Shared.Correlation;

namespace Manager.Address.Services;

/// <summary>
/// Service for validating schema references during entity operations
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

    public async Task ValidateSchemaExists(Guid schemaId)
    {
        if (!_enableSchemaValidation)
        {
            _logger.LogDebugWithCorrelation("Schema validation is disabled. Skipping validation for SchemaId: {SchemaId}", schemaId);
            return;
        }

        if (schemaId == Guid.Empty)
        {
            var message = "SchemaId cannot be empty";
            _logger.LogWarningWithCorrelation("Schema validation failed: {Message}", message);
            throw new InvalidOperationException(message);
        }

        _logger.LogDebugWithCorrelation("Validating schema exists. SchemaId: {SchemaId}", schemaId);

        try
        {
            var schemaExists = await _httpClient.CheckSchemaExists(schemaId);
            
            if (!schemaExists)
            {
                var message = $"Schema with ID {schemaId} does not exist";
                _logger.LogWarningWithCorrelation("Schema validation failed: {Message}", message);
                throw new InvalidOperationException(message);
            }

            _logger.LogDebugWithCorrelation("Schema validation passed. SchemaId: {SchemaId}", schemaId);
        }
        catch (InvalidOperationException)
        {
            // Re-throw validation exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Schema validation failed due to unexpected error. SchemaId: {SchemaId}", schemaId);
            
            // Fail-safe approach: if validation service is unavailable, reject the operation
            throw new InvalidOperationException($"Schema validation service is unavailable. Operation rejected for data integrity.", ex);
        }
    }
}
