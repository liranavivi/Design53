using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace Shared.Entities.Validation;

/// <summary>
/// Validation attribute to ensure a collection is not null or empty.
/// </summary>
public class NotEmptyCollectionAttribute : ValidationAttribute
{
    /// <summary>
    /// Initializes a new instance of the NotEmptyCollectionAttribute class.
    /// </summary>
    public NotEmptyCollectionAttribute() : base("The {0} field cannot be empty.")
    {
    }

    /// <summary>
    /// Determines whether the specified value is valid.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <returns>true if the value is valid; otherwise, false.</returns>
    public override bool IsValid(object? value)
    {
        if (value == null)
            return false;

        if (value is ICollection collection)
        {
            return collection.Count > 0;
        }

        if (value is IEnumerable enumerable)
        {
            return enumerable.Cast<object>().Any();
        }

        return false;
    }

    /// <summary>
    /// Formats the error message that is displayed when validation fails.
    /// </summary>
    /// <param name="name">The name of the field that failed validation.</param>
    /// <returns>The formatted error message.</returns>
    public override string FormatErrorMessage(string name)
    {
        return string.Format(ErrorMessageString, name);
    }
}
