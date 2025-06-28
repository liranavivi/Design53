# Extensible Validation Pattern - Usage Example

This document demonstrates how to add new entity types to the validation system without modifying the shared processor code.

## Current Implementation

The validation system now uses an interface-based approach that automatically handles any entity type that implements the required interfaces.

### Key Interfaces

```csharp
/// <summary>
/// Interface for models that can be validated against a schema
/// </summary>
public interface IValidatableModel
{
    string Payload { get; }
    string SchemaDefinition { get; }
    string Name { get; }
    string Version { get; }
}

/// <summary>
/// Interface for assignment models that contain validatable data
/// </summary>
public interface IValidatableAssignmentModel
{
    Guid EntityId { get; }
    IValidatableModel GetValidatableModel();
}
```

## Adding a New Entity Type (Example: NotificationEntity)

### Step 1: Create the Entity Model

```csharp
/// <summary>
/// Model for notification entity data with schema definition
/// </summary>
public class NotificationModel : IValidatableModel
{
    /// <summary>
    /// Name of the notification entity
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Version of the notification entity
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Payload data for the notification
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Schema definition retrieved from schema manager
    /// </summary>
    public string SchemaDefinition { get; set; } = string.Empty;

    /// <summary>
    /// Notification-specific property: recipient list
    /// </summary>
    public string Recipients { get; set; } = string.Empty;

    /// <summary>
    /// Notification-specific property: delivery method
    /// </summary>
    public string DeliveryMethod { get; set; } = string.Empty;
}
```

### Step 2: Create the Assignment Model

```csharp
/// <summary>
/// Assignment model for notification entities
/// </summary>
public class NotificationAssignmentModel : AssignmentModel, IValidatableAssignmentModel
{
    /// <summary>
    /// Notification entity data
    /// </summary>
    public NotificationModel Notification { get; set; } = new();

    /// <summary>
    /// Gets the validatable model data
    /// </summary>
    public IValidatableModel GetValidatableModel() => Notification;
}
```

### Step 3: Update JSON Polymorphism Registration

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AddressAssignmentModel), "Address")]
[JsonDerivedType(typeof(DeliveryAssignmentModel), "Delivery")]
[JsonDerivedType(typeof(NotificationAssignmentModel), "Notification")] // Add this line
public abstract class AssignmentModel
{
    public Guid EntityId { get; set; }
}
```

### Step 4: Create the Entity (if needed)

```csharp
/// <summary>
/// Represents a notification entity in the system.
/// Contains notification payload information including version, name, and payload data.
/// </summary>
public class NotificationEntity : DeliveryEntity
{
    /// <summary>
    /// Gets or sets the recipient list.
    /// </summary>
    [BsonElement("recipients")]
    [Required(ErrorMessage = "Recipients is required")]
    public string Recipients { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the delivery method.
    /// </summary>
    [BsonElement("deliveryMethod")]
    [Required(ErrorMessage = "DeliveryMethod is required")]
    public string DeliveryMethod { get; set; } = string.Empty;

    /// <summary>
    /// Gets the composite key for this notification entity.
    /// </summary>
    public override string GetCompositeKey() => $"{Version}_{Name}_{Recipients}";
}
```

## What Happens Automatically

Once you add the new entity type following the steps above:

1. **✅ Validation Works Automatically**: The `ValidateAssignmentEntitiesAsync` method will automatically detect and validate `NotificationAssignmentModel` entities without any code changes.

2. **✅ No Processor Code Changes**: The shared processor service doesn't need any modifications.

3. **✅ Type Safety**: The compiler ensures that all validatable entities implement the required interfaces.

4. **✅ Consistent Logging**: All validation logging will work consistently across entity types.

5. **✅ Error Handling**: All error handling and configuration (like `FailOnValidationError`) works the same way.

## Benefits

- **Open/Closed Principle**: Open for extension, closed for modification
- **Single Responsibility**: Each entity type manages its own validation data
- **Interface Segregation**: Only validatable entities implement validation interfaces
- **Dependency Inversion**: Processor depends on abstractions, not concrete types
- **Maintainability**: Clear patterns for adding new entity types
- **Testability**: Easy to mock and test individual components

## Migration Notes

The old `ValidateDeliveryEntitiesAsync` method has been replaced with `ValidateAssignmentEntitiesAsync` which:

- Validates both `DeliveryAssignmentModel` AND `AddressAssignmentModel` entities
- Uses the same validation logic for all entity types
- Automatically handles any new entity types that implement the interfaces
- Maintains backward compatibility with existing functionality

This ensures that both delivery and address entities are properly validated, and any future entity types will be automatically included in the validation process.
