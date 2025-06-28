using Shared.Models;

namespace Shared.Processor.Services;

/// <summary>
/// Interface for collecting processor performance metrics
/// </summary>
public interface IPerformanceMetricsService
{
    /// <summary>
    /// Collects current performance metrics for the processor
    /// </summary>
    /// <returns>Current performance metrics</returns>
    Task<ProcessorPerformanceMetrics> CollectMetricsAsync();

    /// <summary>
    /// Records an activity execution for throughput calculation
    /// </summary>
    /// <param name="success">Whether the activity was successful</param>
    /// <param name="executionTimeMs">Execution time in milliseconds</param>
    void RecordActivity(bool success, double executionTimeMs);

    /// <summary>
    /// Gets the current activity throughput (activities per minute)
    /// </summary>
    /// <returns>Activities per minute</returns>
    double GetCurrentThroughput();

    /// <summary>
    /// Gets the current success rate percentage
    /// </summary>
    /// <returns>Success rate percentage (0-100)</returns>
    double GetSuccessRate();

    /// <summary>
    /// Gets the average execution time in milliseconds
    /// </summary>
    /// <returns>Average execution time in milliseconds</returns>
    double GetAverageExecutionTime();

    /// <summary>
    /// Resets all metrics counters
    /// </summary>
    void Reset();
}
