using System.ComponentModel.DataAnnotations;

namespace Shared.Models;

/// <summary>
/// Configuration model for manager application settings
/// </summary>
public class ManagerConfiguration
{
    /// <summary>
    /// Version of the manager (used in meter naming)
    /// </summary>
    [Required]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Name of the manager (used in meter naming)
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the manager functionality
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets the composite key for this manager
    /// </summary>
    public string GetCompositeKey() => $"{Version}_{Name}";
}
