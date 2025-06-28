using System.Collections.Generic;

namespace Manager.Schema.Services;

/// <summary>
/// Interface for analyzing breaking changes in JSON schema definitions
/// </summary>
public interface ISchemaBreakingChangeAnalyzer
{
    /// <summary>
    /// Determines if the schema update contains breaking changes
    /// </summary>
    /// <param name="existingDefinition">The current schema definition JSON</param>
    /// <param name="updatedDefinition">The updated schema definition JSON</param>
    /// <returns>True if the update contains breaking changes</returns>
    bool IsBreakingChange(string existingDefinition, string updatedDefinition);

    /// <summary>
    /// Gets detailed information about breaking changes
    /// </summary>
    /// <param name="existingDefinition">The current schema definition JSON</param>
    /// <param name="updatedDefinition">The updated schema definition JSON</param>
    /// <returns>List of breaking change descriptions</returns>
    List<string> GetBreakingChangeDetails(string existingDefinition, string updatedDefinition);
}
