using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace Shared.Entities.Validation;

/// <summary>
/// Validation attribute to ensure a collection of GUIDs does not contain any empty GUIDs (Guid.Empty).
/// </summary>
public class NoEmptyGuidsAttribute : ValidationAttribute
{
    /// <summary>
    /// Initializes a new instance of the NoEmptyGuidsAttribute class.
    /// </summary>
    public NoEmptyGuidsAttribute() : base("The {0} field cannot contain empty GUIDs.")
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
            return true; // Let other attributes handle null validation

        if (value is IEnumerable<Guid> guidCollection)
        {
            return !guidCollection.Any(g => g == Guid.Empty);
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is Guid guid && guid == Guid.Empty)
                {
                    return false;
                }
            }
            return true;
        }

        return true; // If it's not a collection, let other validators handle it
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
