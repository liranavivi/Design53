namespace Shared.Exceptions;

/// <summary>
/// Exception thrown when foreign key validation fails during CREATE or UPDATE operations.
/// This occurs when a referenced entity (e.g., ProtocolEntity) does not exist.
/// </summary>
public class ForeignKeyValidationException : Exception
{
    /// <summary>
    /// Gets the type of the entity that failed foreign key validation.
    /// </summary>
    public string EntityType { get; }

    /// <summary>
    /// Gets the name of the foreign key property that failed validation.
    /// </summary>
    public string ForeignKeyProperty { get; }

    /// <summary>
    /// Gets the value of the foreign key that was not found.
    /// </summary>
    public object ForeignKeyValue { get; }

    /// <summary>
    /// Gets the type of the referenced entity that was not found.
    /// </summary>
    public string ReferencedEntityType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForeignKeyValidationException"/> class.
    /// </summary>
    /// <param name="entityType">The type of the entity that failed validation.</param>
    /// <param name="foreignKeyProperty">The name of the foreign key property.</param>
    /// <param name="foreignKeyValue">The value of the foreign key that was not found.</param>
    /// <param name="referencedEntityType">The type of the referenced entity.</param>
    public ForeignKeyValidationException(
        string entityType, 
        string foreignKeyProperty, 
        object foreignKeyValue, 
        string referencedEntityType)
        : base($"Foreign key validation failed for {entityType}.{foreignKeyProperty}. Referenced {referencedEntityType} with ID '{foreignKeyValue}' does not exist.")
    {
        EntityType = entityType;
        ForeignKeyProperty = foreignKeyProperty;
        ForeignKeyValue = foreignKeyValue;
        ReferencedEntityType = referencedEntityType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForeignKeyValidationException"/> class with an inner exception.
    /// </summary>
    /// <param name="entityType">The type of the entity that failed validation.</param>
    /// <param name="foreignKeyProperty">The name of the foreign key property.</param>
    /// <param name="foreignKeyValue">The value of the foreign key that was not found.</param>
    /// <param name="referencedEntityType">The type of the referenced entity.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ForeignKeyValidationException(
        string entityType, 
        string foreignKeyProperty, 
        object foreignKeyValue, 
        string referencedEntityType, 
        Exception innerException)
        : base($"Foreign key validation failed for {entityType}.{foreignKeyProperty}. Referenced {referencedEntityType} with ID '{foreignKeyValue}' does not exist.", innerException)
    {
        EntityType = entityType;
        ForeignKeyProperty = foreignKeyProperty;
        ForeignKeyValue = foreignKeyValue;
        ReferencedEntityType = referencedEntityType;
    }

    /// <summary>
    /// Gets a detailed error message suitable for API responses.
    /// </summary>
    /// <returns>A user-friendly error message for API consumers.</returns>
    public string GetApiErrorMessage()
    {
        return $"The referenced {ReferencedEntityType} with ID '{ForeignKeyValue}' does not exist. Please provide a valid {ForeignKeyProperty}.";
    }
}
