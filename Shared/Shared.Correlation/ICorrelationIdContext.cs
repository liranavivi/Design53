namespace Shared.Correlation;

/// <summary>
/// Provides access to the current correlation ID context.
/// Manages correlation ID storage and retrieval in a thread-safe manner.
/// </summary>
public interface ICorrelationIdContext
{
    /// <summary>
    /// Gets the current correlation ID for the executing context.
    /// Returns Guid.Empty if no correlation ID is set.
    /// </summary>
    Guid Current { get; }

    /// <summary>
    /// Sets the correlation ID for the current executing context.
    /// </summary>
    /// <param name="correlationId">The correlation ID to set.</param>
    void Set(Guid correlationId);

    /// <summary>
    /// Generates a new correlation ID and sets it as the current context.
    /// </summary>
    /// <returns>The newly generated correlation ID.</returns>
    Guid Generate();

    /// <summary>
    /// Clears the current correlation ID context.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets the current correlation ID as a string.
    /// Returns empty string if no correlation ID is set.
    /// </summary>
    string CurrentAsString { get; }
}
