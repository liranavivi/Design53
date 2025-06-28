using Manager.Orchestrator.Models;

namespace Manager.Orchestrator.Services;

/// <summary>
/// Interface for orchestration cache service operations following ProcessorHealthMonitor pattern
/// </summary>
public interface IOrchestrationCacheService
{
    /// <summary>
    /// Stores orchestration data in cache
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID as cache key</param>
    /// <param name="orchestrationData">The complete orchestration data to cache</param>
    /// <param name="ttl">Time-to-live for the cache entry</param>
    /// <returns>Task representing the cache operation</returns>
    Task StoreOrchestrationDataAsync(Guid orchestratedFlowId, OrchestrationCacheModel orchestrationData, TimeSpan? ttl = null);

    /// <summary>
    /// Retrieves orchestration data from cache
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID as cache key</param>
    /// <returns>Cached orchestration data or null if not found/expired</returns>
    Task<OrchestrationCacheModel?> GetOrchestrationDataAsync(Guid orchestratedFlowId);

    /// <summary>
    /// Removes orchestration data from cache
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID as cache key</param>
    /// <returns>Task representing the cache operation</returns>
    Task RemoveOrchestrationDataAsync(Guid orchestratedFlowId);

    /// <summary>
    /// Checks if orchestration data exists in cache and is not expired
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID as cache key</param>
    /// <returns>True if data exists and is valid, false otherwise</returns>
    Task<bool> ExistsAndValidAsync(Guid orchestratedFlowId);
}
