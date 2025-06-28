namespace Shared.Exceptions;

/// <summary>
/// Exception thrown when a duplicate key constraint is violated during database operations.
/// This typically occurs when attempting to create an entity with a key that already exists.
/// </summary>
public class DuplicateKeyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateKeyException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DuplicateKeyException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateKeyException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DuplicateKeyException(string message, Exception innerException) : base(message, innerException) { }
}
