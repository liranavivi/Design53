namespace Shared.Services;

/// <summary>
/// Interface for manager-specific metrics service.
/// Provides comprehensive manager operation metrics for analysis and monitoring.
/// </summary>
public interface IManagerMetricsService
{
    /// <summary>
    /// Records request processing completion metrics.
    /// </summary>
    /// <param name="success">Whether the request was successful</param>
    /// <param name="duration">Duration of the request processing</param>
    /// <param name="operation">The operation type (e.g., "create", "update", "delete", "get")</param>
    /// <param name="entityType">The entity type being operated on (optional)</param>
    void RecordRequestProcessed(bool success, TimeSpan duration, string operation, string? entityType = null);

    /// <summary>
    /// Records entity operation metrics.
    /// </summary>
    /// <param name="operation">The operation type (e.g., "create", "update", "delete", "query")</param>
    /// <param name="entityType">The entity type being operated on</param>
    /// <param name="count">Number of entities affected (default: 1)</param>
    void RecordEntityOperation(string operation, string entityType, int count = 1);

    /// <summary>
    /// Records validation metrics.
    /// </summary>
    /// <param name="success">Whether the validation was successful</param>
    /// <param name="duration">Duration of the validation operation</param>
    /// <param name="validationType">The type of validation performed</param>
    void RecordValidation(bool success, TimeSpan duration, string validationType);

    /// <summary>
    /// Records health status.
    /// </summary>
    /// <param name="status">Health status (0=Healthy, 1=Degraded, 2=Unhealthy)</param>
    void RecordHealthStatus(int status);

    /// <summary>
    /// Records a generic operation with timing and success status.
    /// </summary>
    /// <param name="operation">The operation type</param>
    /// <param name="entityType">The entity type being operated on</param>
    /// <param name="duration">Duration of the operation</param>
    /// <param name="success">Whether the operation was successful</param>
    void RecordOperation(string operation, string entityType, TimeSpan duration, bool success);
}
