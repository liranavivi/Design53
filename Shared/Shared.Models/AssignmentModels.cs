using System.Text.Json.Serialization;

namespace Shared.Models;

/// <summary>
/// Interface for models that can be validated against a schema
/// </summary>
public interface IValidatableModel
{
    /// <summary>
    /// The payload data to validate
    /// </summary>
    string Payload { get; }

    /// <summary>
    /// The schema definition to validate against
    /// </summary>
    string SchemaDefinition { get; }

    /// <summary>
    /// Name for logging purposes
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Version for logging purposes
    /// </summary>
    string Version { get; }
}

/// <summary>
/// Interface for assignment models that contain validatable data
/// </summary>
public interface IValidatableAssignmentModel
{
    /// <summary>
    /// Entity ID of the assignment
    /// </summary>
    Guid EntityId { get; }

    /// <summary>
    /// Gets the validatable model data
    /// </summary>
    IValidatableModel GetValidatableModel();
}

/// <summary>
/// Base model for assignment entities
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AddressAssignmentModel), "Address")]
[JsonDerivedType(typeof(DeliveryAssignmentModel), "Delivery")]
public abstract class AssignmentModel
{
    /// <summary>
    /// Entity ID of the assignment
    /// </summary>
    public Guid EntityId { get; set; }
}

/// <summary>
/// Assignment model for address entities
/// </summary>
public class AddressAssignmentModel : AssignmentModel, IValidatableAssignmentModel
{
    /// <summary>
    /// Address entity data
    /// </summary>
    public AddressModel Address { get; set; } = new();

    /// <summary>
    /// Gets the validatable model data
    /// </summary>
    public IValidatableModel GetValidatableModel() => Address;
}

/// <summary>
/// Assignment model for delivery entities
/// </summary>
public class DeliveryAssignmentModel : AssignmentModel, IValidatableAssignmentModel
{
    /// <summary>
    /// Delivery entity data
    /// </summary>
    public DeliveryModel Delivery { get; set; } = new();

    /// <summary>
    /// Gets the validatable model data
    /// </summary>
    public IValidatableModel GetValidatableModel() => Delivery;
}

/// <summary>
/// Model for delivery entity data with schema definition
/// </summary>
public class DeliveryModel : IValidatableModel
{
    /// <summary>
    /// Name of the delivery entity
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Version of the delivery entity
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Payload data for the delivery
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Schema definition retrieved from schema manager
    /// </summary>
    public string SchemaDefinition { get; set; } = string.Empty;
}

/// <summary>
/// Model for address entity data with schema definition
/// </summary>
public class AddressModel: DeliveryModel
{
    /// <summary>
    /// Connection string for the address
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

}

