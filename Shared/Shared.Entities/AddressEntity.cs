using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Shared.Entities;

/// <summary>
/// Represents a address entity in the system.
/// Contains Address definition information including version, name, and JSON Address definition.
/// </summary>
public class AddressEntity : DeliveryEntity
{
    /// <summary>
    /// Gets or sets the connection string value.
    /// This provides connection information for the address entity.
    /// </summary>
    [BsonElement("connectionString")]
    [Required(ErrorMessage = "ConnectionString is required")]
    public string ConnectionString { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets the composite key for this address entity.
    /// The composite key is formed by connection string.
    /// </summary>
    /// <returns>A string in the format "ConnectionString" that uniquely identifies this address.</returns>
    public override string GetCompositeKey() => $"{ConnectionString}";
}
