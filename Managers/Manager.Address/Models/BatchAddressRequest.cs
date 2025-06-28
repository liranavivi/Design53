using System.ComponentModel.DataAnnotations;

namespace Manager.Address.Models;

/// <summary>
/// Request model for batch address operations
/// </summary>
public class BatchAddressRequest
{
    /// <summary>
    /// List of entity IDs to retrieve as addresses
    /// </summary>
    [Required]
    public List<Guid> EntityIds { get; set; } = new();
}

/// <summary>
/// Response model for batch address operations
/// </summary>
public class BatchAddressResponse
{
    /// <summary>
    /// Successfully retrieved addresses
    /// </summary>
    public List<Shared.Entities.AddressEntity> Addresses { get; set; } = new();

    /// <summary>
    /// IDs that were not found
    /// </summary>
    public List<Guid> NotFound { get; set; } = new();

    /// <summary>
    /// Total number of requested IDs
    /// </summary>
    public int RequestedCount { get; set; }

    /// <summary>
    /// Number of successfully retrieved addresses
    /// </summary>
    public int FoundCount { get; set; }
}
