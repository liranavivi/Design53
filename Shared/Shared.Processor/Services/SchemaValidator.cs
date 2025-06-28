using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Correlation;
using Shared.Extensions;
using Shared.Processor.Models;

namespace Shared.Processor.Services;

/// <summary>
/// JSON schema validator implementation using System.Text.Json with basic validation
/// </summary>
public class SchemaValidator : ISchemaValidator
{
    private readonly ILogger<SchemaValidator> _logger;
    private readonly SchemaValidationConfiguration _config;
    private readonly ActivitySource _activitySource;
    private readonly Dictionary<string, JsonDocument> _schemaCache;
    private readonly object _schemaCacheLock = new();
    private readonly IProcessorHealthMetricsService? _healthMetricsService;

    public SchemaValidator(
        ILogger<SchemaValidator> logger,
        IOptions<SchemaValidationConfiguration> config,
        IProcessorHealthMetricsService? healthMetricsService = null)
    {
        _logger = logger;
        _config = config.Value;
        _activitySource = new ActivitySource(ActivitySources.Validation);
        _schemaCache = new Dictionary<string, JsonDocument>();
        _healthMetricsService = healthMetricsService;
    }

    public async Task<bool> ValidateAsync(string jsonData, string jsonSchema)
    {
        var result = await ValidateWithDetailsAsync(jsonData, jsonSchema);
        return result.IsValid;
    }

    public async Task<SchemaValidationResult> ValidateWithDetailsAsync(string jsonData, string jsonSchema)
    {
        using var activity = _activitySource.StartActivity("ValidateSchema");
        var stopwatch = Stopwatch.StartNew();

        var result = new SchemaValidationResult();

        try
        {
            // Parse and cache schema
            var schema = await GetOrCreateSchemaAsync(jsonSchema);

            // Parse JSON data
            JsonDocument jsonDocument;
            try
            {
                jsonDocument = JsonDocument.Parse(jsonData);
            }
            catch (JsonException ex)
            {
                // Record JSON parsing exception
                _healthMetricsService?.RecordException(ex.GetType().Name, "error", isCritical: false);

                result.IsValid = false;
                result.Errors.Add($"Invalid JSON format: {ex.Message}");
                result.ErrorPath = ex.Path;

                activity?.SetValidationTags(true, false, 1, "unknown", ex.Path);

                if (_config.LogValidationErrors)
                {
                    _logger.LogErrorWithCorrelation(ex, "JSON parsing failed during validation");
                }

                return result;
            }

            // Perform basic validation (structure and type checking)
            var validationErrors = new List<string>();
            var isValid = ValidateJsonStructure(jsonDocument, schema, validationErrors);

            result.IsValid = isValid;
            result.Errors = validationErrors;
            
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            // Set telemetry tags
            activity?.SetValidationTags(
                true, 
                isValid, 
                validationErrors.Count, 
                "json_schema",
                result.ErrorPath);

            // Log results based on configuration
            if (isValid)
            {
                _logger.LogDebugWithCorrelation("Schema validation passed. Duration: {Duration}ms", stopwatch.ElapsedMilliseconds);
            }
            else
            {
                if (_config.LogValidationErrors)
                {
                    _logger.LogErrorWithCorrelation(
                        "Schema validation failed. Errors: {ErrorCount}, Duration: {Duration}ms, Errors: {Errors}",
                        validationErrors.Count, stopwatch.ElapsedMilliseconds, string.Join("; ", validationErrors));
                }
                else if (_config.LogValidationWarnings)
                {
                    _logger.LogWarningWithCorrelation(
                        "Schema validation failed. Errors: {ErrorCount}, Duration: {Duration}ms",
                        validationErrors.Count, stopwatch.ElapsedMilliseconds);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.IsValid = false;
            result.Errors.Add($"Validation error: {ex.Message}");

            // Record schema validation exception
            _healthMetricsService?.RecordException(ex.GetType().Name, "error", isCritical: false);

            activity?.SetErrorTags(ex)
                    ?.SetValidationTags(true, false, 1, "json_schema");

            _logger.LogErrorWithCorrelation(ex, "Schema validation failed with exception. Duration: {Duration}ms", stopwatch.ElapsedMilliseconds);

            return result;
        }
    }

    private async Task<JsonDocument> GetOrCreateSchemaAsync(string jsonSchema)
    {
        // Create a cache key based on schema content hash
        var schemaHash = jsonSchema.GetHashCode().ToString();

        lock (_schemaCacheLock)
        {
            if (_schemaCache.TryGetValue(schemaHash, out var cachedSchema))
            {
                return cachedSchema;
            }
        }

        // Parse schema (this is CPU-bound, so we'll run it on a background thread)
        var schema = await Task.Run(() =>
        {
            try
            {
                return JsonDocument.Parse(jsonSchema);
            }
            catch (Exception ex)
            {
                // Record schema parsing exception
                _healthMetricsService?.RecordException(ex.GetType().Name, "error", isCritical: true);

                _logger.LogErrorWithCorrelation(ex, "Failed to parse JSON schema");
                throw new InvalidOperationException($"Invalid JSON schema: {ex.Message}", ex);
            }
        });

        // Cache the parsed schema
        lock (_schemaCacheLock)
        {
            if (!_schemaCache.ContainsKey(schemaHash))
            {
                _schemaCache[schemaHash] = schema;

                // Limit cache size to prevent memory issues
                if (_schemaCache.Count > 100)
                {
                    var oldestKey = _schemaCache.Keys.First();
                    _schemaCache.Remove(oldestKey);
                    _logger.LogDebugWithCorrelation("Removed oldest schema from cache. Key: {Key}", oldestKey);
                }
            }
        }

        return schema;
    }

    /// <summary>
    /// Performs basic JSON structure validation without using paid schema libraries
    /// </summary>
    private bool ValidateJsonStructure(JsonDocument data, JsonDocument schema, List<string> errors)
    {
        try
        {
            // Basic validation: ensure both are valid JSON documents
            if (data == null)
            {
                errors.Add("Data document is null");
                return false;
            }

            if (schema == null)
            {
                errors.Add("Schema document is null");
                return false;
            }

            // For now, we'll do basic validation:
            // 1. Check if data is valid JSON (already done by parsing)
            // 2. Check basic structure compatibility

            var dataRoot = data.RootElement;
            var schemaRoot = schema.RootElement;

            // Basic type validation
            if (schemaRoot.TryGetProperty("type", out var typeProperty))
            {
                var expectedType = typeProperty.GetString();
                if (expectedType != null && !ValidateJsonType(dataRoot, expectedType, errors))
                {
                    return false;
                }
            }

            // Basic required properties validation (if schema has "required" array)
            if (schemaRoot.TryGetProperty("required", out var requiredProperty) &&
                requiredProperty.ValueKind == JsonValueKind.Array)
            {
                if (!ValidateRequiredProperties(dataRoot, requiredProperty, errors))
                {
                    return false;
                }
            }

            // If we get here, basic validation passed
            _logger.LogDebugWithCorrelation("Basic JSON structure validation passed");
            return true;
        }
        catch (Exception ex)
        {
            // Record JSON structure validation exception
            _healthMetricsService?.RecordException(ex.GetType().Name, "error", isCritical: false);

            errors.Add($"Validation error: {ex.Message}");
            _logger.LogErrorWithCorrelation(ex, "Error during JSON structure validation");
            return false;
        }
    }

    private bool ValidateJsonType(JsonElement element, string expectedType, List<string> errors)
    {
        return expectedType.ToLower() switch
        {
            "object" => element.ValueKind == JsonValueKind.Object,
            "array" => element.ValueKind == JsonValueKind.Array,
            "string" => element.ValueKind == JsonValueKind.String,
            "number" => element.ValueKind == JsonValueKind.Number,
            "boolean" => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False,
            "null" => element.ValueKind == JsonValueKind.Null,
            _ => true // Unknown type, assume valid
        };
    }

    private bool ValidateRequiredProperties(JsonElement dataElement, JsonElement requiredArray, List<string> errors)
    {
        if (dataElement.ValueKind != JsonValueKind.Object)
        {
            return true; // Not an object, skip property validation
        }

        foreach (var requiredProperty in requiredArray.EnumerateArray())
        {
            if (requiredProperty.ValueKind == JsonValueKind.String)
            {
                var propertyName = requiredProperty.GetString();
                if (!string.IsNullOrEmpty(propertyName) && !dataElement.TryGetProperty(propertyName, out _))
                {
                    errors.Add($"Required property '{propertyName}' is missing");
                    return false;
                }
            }
        }

        return true;
    }

    public void Dispose()
    {
        // Dispose cached schemas
        lock (_schemaCacheLock)
        {
            foreach (var schema in _schemaCache.Values)
            {
                schema?.Dispose();
            }
            _schemaCache.Clear();
        }

        _activitySource?.Dispose();
    }
}
