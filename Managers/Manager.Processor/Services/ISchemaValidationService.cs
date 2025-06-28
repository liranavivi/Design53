using System;
using System.Threading.Tasks;

namespace Manager.Processor.Services;

/// <summary>
/// Interface for validating schema references during processor entity operations
/// </summary>
public interface ISchemaValidationService
{
    /// <summary>
    /// Validates that both input and output schemas exist before creating or updating a processor entity
    /// </summary>
    /// <param name="inputSchemaId">The input schema ID to validate</param>
    /// <param name="outputSchemaId">The output schema ID to validate</param>
    /// <returns>Task that completes successfully if both schemas exist</returns>
    /// <exception cref="InvalidOperationException">Thrown if either schema doesn't exist or validation fails</exception>
    Task ValidateSchemasExist(Guid inputSchemaId, Guid outputSchemaId);
}
