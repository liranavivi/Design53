namespace Shared.Processor.Services;
using Shared.Models;
/// <summary>
/// Interface for the processor health monitoring service
/// </summary>
public interface IProcessorHealthMonitor
{
    /// <summary>
    /// Starts the health monitoring background service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the monitoring operation</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the health monitoring background service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the stop operation</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a single health check and updates the cache
    /// </summary>
    /// <returns>Task representing the health check operation</returns>
    Task PerformHealthCheckAsync();

    /// <summary>
    /// Gets the current health status from cache
    /// </summary>
    /// <param name="processorId">ID of the processor to check</param>
    /// <returns>Health cache entry or null if not found</returns>
    Task<ProcessorHealthCacheEntry?> GetHealthStatusFromCacheAsync(Guid processorId);

    /// <summary>
    /// Gets health status for all processors from cache
    /// </summary>
    /// <returns>Dictionary of processor health entries</returns>
    Task<Dictionary<Guid, ProcessorHealthCacheEntry>> GetAllHealthStatusFromCacheAsync();

    /// <summary>
    /// Gets statistics about the health monitoring system itself
    /// </summary>
    /// <returns>Health monitoring statistics for this pod</returns>
    Models.HealthMonitoringStatistics GetMonitoringStatistics();
}
