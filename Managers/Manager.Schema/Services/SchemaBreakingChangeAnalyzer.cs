using System.Text.Json;
using Shared.Correlation;

namespace Manager.Schema.Services;

/// <summary>
/// Service for analyzing breaking changes in JSON schema definitions
/// </summary>
public class SchemaBreakingChangeAnalyzer : ISchemaBreakingChangeAnalyzer
{
    private readonly ILogger<SchemaBreakingChangeAnalyzer> _logger;

    public SchemaBreakingChangeAnalyzer(ILogger<SchemaBreakingChangeAnalyzer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsBreakingChange(string existingDefinition, string updatedDefinition)
    {
        try
        {
            var existing = JsonSerializer.Deserialize<JsonElement>(existingDefinition);
            var updated = JsonSerializer.Deserialize<JsonElement>(updatedDefinition);

            return HasBreakingChanges(existing, updated);
        }
        catch (Exception ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Failed to parse schema definitions for breaking change analysis. Treating as breaking change for safety.");
            return true; // Fail-safe: treat unparseable schemas as breaking changes
        }
    }

    public List<string> GetBreakingChangeDetails(string existingDefinition, string updatedDefinition)
    {
        var changes = new List<string>();

        try
        {
            var existing = JsonSerializer.Deserialize<JsonElement>(existingDefinition);
            var updated = JsonSerializer.Deserialize<JsonElement>(updatedDefinition);

            AnalyzeBreakingChanges(existing, updated, changes);
        }
        catch (Exception ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Failed to analyze breaking changes in schema definitions.");
            changes.Add("Unable to parse schema definitions - treating as potentially breaking change");
        }

        return changes.Any() ? changes : new List<string> { "Potential breaking changes detected in schema definition" };
    }

    private bool HasBreakingChanges(JsonElement existing, JsonElement updated)
    {
        // Check for new required fields
        if (HasNewRequiredFields(existing, updated))
            return true;

        // Check for removed required fields
        if (HasRemovedRequiredFields(existing, updated))
            return true;

        // Check for incompatible type changes
        if (HasIncompatibleTypeChanges(existing, updated))
            return true;

        // Check for removed properties
        if (HasRemovedProperties(existing, updated))
            return true;

        // Check for stricter validation rules
        if (HasStricterValidation(existing, updated))
            return true;

        return false;
    }

    private bool HasNewRequiredFields(JsonElement existing, JsonElement updated)
    {
        var existingRequired = GetRequiredFields(existing);
        var updatedRequired = GetRequiredFields(updated);

        // New required fields are breaking changes
        return updatedRequired.Except(existingRequired).Any();
    }

    private bool HasRemovedRequiredFields(JsonElement existing, JsonElement updated)
    {
        var existingRequired = GetRequiredFields(existing);
        var updatedRequired = GetRequiredFields(updated);

        // Removed required fields are breaking changes
        return existingRequired.Except(updatedRequired).Any();
    }

    private bool HasIncompatibleTypeChanges(JsonElement existing, JsonElement updated)
    {
        var existingProperties = GetProperties(existing);
        var updatedProperties = GetProperties(updated);

        foreach (var prop in existingProperties.Keys.Intersect(updatedProperties.Keys))
        {
            var existingType = GetPropertyType(existingProperties[prop]);
            var updatedType = GetPropertyType(updatedProperties[prop]);

            if (!AreTypesCompatible(existingType, updatedType))
                return true;
        }

        return false;
    }

    private bool HasRemovedProperties(JsonElement existing, JsonElement updated)
    {
        var existingProperties = GetProperties(existing);
        var updatedProperties = GetProperties(updated);

        // Removed properties are breaking changes
        return existingProperties.Keys.Except(updatedProperties.Keys).Any();
    }

    private bool HasStricterValidation(JsonElement existing, JsonElement updated)
    {
        // Check for stricter validation rules like:
        // - Reduced maxLength
        // - Increased minLength
        // - More restrictive patterns
        // - Reduced enum values
        // This is a simplified implementation
        return false;
    }

    private HashSet<string> GetRequiredFields(JsonElement schema)
    {
        var required = new HashSet<string>();

        if (schema.TryGetProperty("required", out var requiredElement) && 
            requiredElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in requiredElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    required.Add(item.GetString()!);
                }
            }
        }

        return required;
    }

    private Dictionary<string, JsonElement> GetProperties(JsonElement schema)
    {
        var properties = new Dictionary<string, JsonElement>();

        if (schema.TryGetProperty("properties", out var propertiesElement) && 
            propertiesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in propertiesElement.EnumerateObject())
            {
                properties[prop.Name] = prop.Value;
            }
        }

        return properties;
    }

    private string GetPropertyType(JsonElement property)
    {
        if (property.TryGetProperty("type", out var typeElement) && 
            typeElement.ValueKind == JsonValueKind.String)
        {
            return typeElement.GetString()!;
        }

        return "unknown";
    }

    private bool AreTypesCompatible(string existingType, string updatedType)
    {
        // Same type is always compatible
        if (existingType == updatedType)
            return true;

        // Define compatible type transitions
        var compatibleTransitions = new Dictionary<string, HashSet<string>>
        {
            ["integer"] = new HashSet<string> { "number" }, // integer can become number
            ["string"] = new HashSet<string>(), // string changes are usually breaking
            ["boolean"] = new HashSet<string>(), // boolean changes are breaking
            ["array"] = new HashSet<string>(), // array changes need deeper analysis
            ["object"] = new HashSet<string>() // object changes need deeper analysis
        };

        return compatibleTransitions.ContainsKey(existingType) && 
               compatibleTransitions[existingType].Contains(updatedType);
    }

    private void AnalyzeBreakingChanges(JsonElement existing, JsonElement updated, List<string> changes)
    {
        // Analyze new required fields
        var existingRequired = GetRequiredFields(existing);
        var updatedRequired = GetRequiredFields(updated);
        var newRequired = updatedRequired.Except(existingRequired);
        
        foreach (var field in newRequired)
        {
            changes.Add($"New required field added: '{field}'");
        }

        // Analyze removed required fields
        var removedRequired = existingRequired.Except(updatedRequired);
        foreach (var field in removedRequired)
        {
            changes.Add($"Required field removed: '{field}'");
        }

        // Analyze removed properties
        var existingProperties = GetProperties(existing);
        var updatedProperties = GetProperties(updated);
        var removedProperties = existingProperties.Keys.Except(updatedProperties.Keys);
        
        foreach (var prop in removedProperties)
        {
            changes.Add($"Property removed: '{prop}'");
        }

        // Analyze type changes
        foreach (var prop in existingProperties.Keys.Intersect(updatedProperties.Keys))
        {
            var existingType = GetPropertyType(existingProperties[prop]);
            var updatedType = GetPropertyType(updatedProperties[prop]);

            if (!AreTypesCompatible(existingType, updatedType))
            {
                changes.Add($"Incompatible type change for property '{prop}': {existingType} → {updatedType}");
            }
        }
    }
}
