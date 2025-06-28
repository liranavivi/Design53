namespace Shared.Processor.Services;

/// <summary>
/// Interface for JSON schema validation
/// </summary>
public interface ISchemaValidator
{
    /// <summary>
    /// Validates JSON data against a JSON schema
    /// </summary>
    /// <param name="jsonData">JSON data to validate</param>
    /// <param name="jsonSchema">JSON schema to validate against</param>
    /// <returns>True if valid, false otherwise</returns>
    Task<bool> ValidateAsync(string jsonData, string jsonSchema);

    /// <summary>
    /// Validates JSON data against a JSON schema and returns detailed validation results
    /// </summary>
    /// <param name="jsonData">JSON data to validate</param>
    /// <param name="jsonSchema">JSON schema to validate against</param>
    /// <returns>Validation result with details</returns>
    Task<SchemaValidationResult> ValidateWithDetailsAsync(string jsonData, string jsonSchema);
}

/// <summary>
/// Result of schema validation with detailed information
/// </summary>
public class SchemaValidationResult
{
    /// <summary>
    /// Whether the validation passed
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation errors
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// List of validation warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Path where the first error occurred
    /// </summary>
    public string? ErrorPath { get; set; }

    /// <summary>
    /// Duration of the validation process
    /// </summary>
    public TimeSpan Duration { get; set; }
}
