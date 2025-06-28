using Shared.Correlation;


namespace Manager.Schema.Services;

/// <summary>
/// Service for validating schema references across all entity managers
/// </summary>
public class SchemaReferenceValidator : ISchemaReferenceValidator
{
    private readonly IManagerHttpClient _managerHttpClient;
    private readonly ILogger<SchemaReferenceValidator> _logger;

    public SchemaReferenceValidator(IManagerHttpClient managerHttpClient, ILogger<SchemaReferenceValidator> logger)
    {
        _managerHttpClient = managerHttpClient ?? throw new ArgumentNullException(nameof(managerHttpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> HasReferences(Guid schemaId)
    {
        _logger.LogInformationWithCorrelation("Starting comprehensive schema reference check for SchemaId: {SchemaId}", schemaId);

        try
        {
            var details = await GetReferenceDetails(schemaId);
            
            _logger.LogInformationWithCorrelation("Completed schema reference check for SchemaId: {SchemaId}. HasReferences: {HasReferences}", 
                schemaId, details.HasAnyReferences);

            return details.HasAnyReferences;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error during schema reference check for SchemaId: {SchemaId}", schemaId);
            throw;
        }
    }

    public async Task<SchemaReferenceDetails> GetReferenceDetails(Guid schemaId)
    {
        _logger.LogDebugWithCorrelation("Getting detailed schema reference information for SchemaId: {SchemaId}", schemaId);

        var details = new SchemaReferenceDetails
        {
            SchemaId = schemaId,
            CheckedAt = DateTime.UtcNow
        };

        var failedChecks = new List<string>();

        // Execute all reference checks in parallel for better performance
        var tasks = new[]
        {
            CheckAddressReferencesAsync(schemaId, details, failedChecks),
            CheckDeliveryReferencesAsync(schemaId, details, failedChecks),
            CheckProcessorInputReferencesAsync(schemaId, details, failedChecks),
            CheckProcessorOutputReferencesAsync(schemaId, details, failedChecks)
        };

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "One or more reference checks failed for SchemaId: {SchemaId}", schemaId);
            // Continue processing - individual task exceptions are handled in their respective methods
        }

        details.FailedChecks = failedChecks.ToArray();

        // If any checks failed, we follow fail-safe approach and assume there are references
        if (failedChecks.Any())
        {
            _logger.LogWarningWithCorrelation("Schema reference validation incomplete for SchemaId: {SchemaId}. Failed checks: {FailedChecks}. " +
                             "Following fail-safe approach - assuming references exist.", 
                             schemaId, string.Join(", ", failedChecks));
            
            throw new InvalidOperationException($"Schema reference validation failed for one or more services: {string.Join(", ", failedChecks)}. " +
                                              "Cannot safely proceed with schema operation.");
        }

        _logger.LogDebugWithCorrelation("Schema reference details for SchemaId: {SchemaId} - Address: {Address}, Delivery: {Delivery}, " +
                        "ProcessorInput: {ProcessorInput}, ProcessorOutput: {ProcessorOutput}", 
                        schemaId, details.HasAddressReferences, details.HasDeliveryReferences, 
                        details.HasProcessorInputReferences, details.HasProcessorOutputReferences);

        return details;
    }

    private async Task CheckAddressReferencesAsync(Guid schemaId, SchemaReferenceDetails details, List<string> failedChecks)
    {
        try
        {
            details.HasAddressReferences = await _managerHttpClient.CheckAddressSchemaReferences(schemaId);
            _logger.LogDebugWithCorrelation("Address reference check completed for SchemaId: {SchemaId}. HasReferences: {HasReferences}", 
                schemaId, details.HasAddressReferences);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Address reference check failed for SchemaId: {SchemaId}", schemaId);
            failedChecks.Add("Address");
        }
    }

    private async Task CheckDeliveryReferencesAsync(Guid schemaId, SchemaReferenceDetails details, List<string> failedChecks)
    {
        try
        {
            details.HasDeliveryReferences = await _managerHttpClient.CheckDeliverySchemaReferences(schemaId);
            _logger.LogDebugWithCorrelation("Delivery reference check completed for SchemaId: {SchemaId}. HasReferences: {HasReferences}", 
                schemaId, details.HasDeliveryReferences);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Delivery reference check failed for SchemaId: {SchemaId}", schemaId);
            failedChecks.Add("Delivery");
        }
    }

    private async Task CheckProcessorInputReferencesAsync(Guid schemaId, SchemaReferenceDetails details, List<string> failedChecks)
    {
        try
        {
            details.HasProcessorInputReferences = await _managerHttpClient.CheckProcessorInputSchemaReferences(schemaId);
            _logger.LogDebugWithCorrelation("Processor input reference check completed for SchemaId: {SchemaId}. HasReferences: {HasReferences}", 
                schemaId, details.HasProcessorInputReferences);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Processor input reference check failed for SchemaId: {SchemaId}", schemaId);
            failedChecks.Add("ProcessorInput");
        }
    }

    private async Task CheckProcessorOutputReferencesAsync(Guid schemaId, SchemaReferenceDetails details, List<string> failedChecks)
    {
        try
        {
            details.HasProcessorOutputReferences = await _managerHttpClient.CheckProcessorOutputSchemaReferences(schemaId);
            _logger.LogDebugWithCorrelation("Processor output reference check completed for SchemaId: {SchemaId}. HasReferences: {HasReferences}", 
                schemaId, details.HasProcessorOutputReferences);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Processor output reference check failed for SchemaId: {SchemaId}", schemaId);
            failedChecks.Add("ProcessorOutput");
        }
    }
}
