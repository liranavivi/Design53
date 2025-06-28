using System.Diagnostics;

namespace Shared.Correlation;

/// <summary>
/// Thread-safe implementation of correlation ID context using AsyncLocal storage.
/// Integrates with OpenTelemetry Activity for automatic trace correlation.
/// </summary>
public class CorrelationIdContext : ICorrelationIdContext
{
    private static readonly AsyncLocal<Guid> _correlationId = new();

    /// <summary>
    /// Gets the current correlation ID for the executing context.
    /// First checks AsyncLocal storage, then falls back to current Activity baggage.
    /// </summary>
    public Guid Current
    {
        get
        {
            // First check AsyncLocal storage
            var localCorrelationId = _correlationId.Value;
            if (localCorrelationId != Guid.Empty)
            {
                return localCorrelationId;
            }

            // Fall back to Activity baggage if available
            var activity = Activity.Current;
            if (activity?.GetBaggageItem("correlation.id") is string baggageCorrelationId &&
                Guid.TryParse(baggageCorrelationId, out var activityCorrelationId))
            {
                return activityCorrelationId;
            }

            return Guid.Empty;
        }
    }

    /// <summary>
    /// Gets the current correlation ID as a string.
    /// Returns empty string if no correlation ID is set.
    /// </summary>
    public string CurrentAsString => Current == Guid.Empty ? string.Empty : Current.ToString();

    /// <summary>
    /// Sets the correlation ID for the current executing context.
    /// Updates both AsyncLocal storage and current Activity baggage.
    /// </summary>
    /// <param name="correlationId">The correlation ID to set.</param>
    public void Set(Guid correlationId)
    {
        _correlationId.Value = correlationId;

        // Also set in Activity baggage for cross-process propagation
        var activity = Activity.Current;
        if (activity != null && correlationId != Guid.Empty)
        {
            activity.SetBaggage("correlation.id", correlationId.ToString());
        }
    }

    /// <summary>
    /// Generates a new correlation ID and sets it as the current context.
    /// </summary>
    /// <returns>The newly generated correlation ID.</returns>
    public Guid Generate()
    {
        var newCorrelationId = Guid.NewGuid();
        Set(newCorrelationId);
        return newCorrelationId;
    }

    /// <summary>
    /// Clears the current correlation ID context.
    /// </summary>
    public void Clear()
    {
        _correlationId.Value = Guid.Empty;
    }

    /// <summary>
    /// Static method to get current correlation ID for use by static extension methods.
    /// Uses the same logic as the Current property.
    /// </summary>
    /// <returns>The current correlation ID or Guid.Empty if none is set.</returns>
    public static Guid GetCurrentCorrelationIdStatic()
    {
        // First check AsyncLocal storage
        var localCorrelationId = _correlationId.Value;
        if (localCorrelationId != Guid.Empty)
        {
            return localCorrelationId;
        }

        // Fall back to Activity baggage if available
        var activity = Activity.Current;
        if (activity?.GetBaggageItem("correlation.id") is string baggageCorrelationId &&
            Guid.TryParse(baggageCorrelationId, out var activityCorrelationId))
        {
            return activityCorrelationId;
        }

        return Guid.Empty;
    }

    /// <summary>
    /// Static method to set correlation ID for use by background services.
    /// </summary>
    /// <param name="correlationId">The correlation ID to set.</param>
    public static void SetCorrelationIdStatic(Guid correlationId)
    {
        _correlationId.Value = correlationId;

        // Also set in Activity baggage for cross-process propagation
        var activity = Activity.Current;
        if (activity != null && correlationId != Guid.Empty)
        {
            activity.SetBaggage("correlation.id", correlationId.ToString());
        }
    }
}
