namespace Shared.Services;
/// <summary>
/// Interface for cache service operations
/// </summary>
public interface ICacheService : IDisposable
{
    /// <summary>
    /// Retrieves data from cache
    /// </summary>
    /// <param name="mapName">Name of the cache map</param>
    /// <param name="key">Cache key</param>
    /// <returns>Cached data or null if not found</returns>
    Task<string?> GetAsync(string mapName, string key);

    /// <summary>
    /// Stores data in cache
    /// </summary>
    /// <param name="mapName">Name of the cache map</param>
    /// <param name="key">Cache key</param>
    /// <param name="value">Data to store</param>
    /// <returns>Task representing the operation</returns>
    Task SetAsync(string mapName, string key, string value);

    /// <summary>
    /// Stores data in cache with time-to-live (TTL)
    /// </summary>
    /// <param name="mapName">Name of the cache map</param>
    /// <param name="key">Cache key</param>
    /// <param name="value">Data to store</param>
    /// <param name="ttl">Time-to-live for the cache entry</param>
    /// <returns>Task representing the operation</returns>
    Task SetAsync(string mapName, string key, string value, TimeSpan ttl);

    /// <summary>
    /// Checks if a key exists in cache
    /// </summary>
    /// <param name="mapName">Name of the cache map</param>
    /// <param name="key">Cache key</param>
    /// <returns>True if key exists, false otherwise</returns>
    Task<bool> ExistsAsync(string mapName, string key);

    /// <summary>
    /// Removes data from cache
    /// </summary>
    /// <param name="mapName">Name of the cache map</param>
    /// <param name="key">Cache key</param>
    /// <returns>Task representing the operation</returns>
    Task RemoveAsync(string mapName, string key);

    /// <summary>
    /// Checks if the cache service is healthy and accessible
    /// </summary>
    /// <returns>True if healthy, false otherwise</returns>
    Task<bool> IsHealthyAsync();

    /// <summary>
    /// Gets cache statistics for a specific map
    /// </summary>
    /// <param name="mapName">Name of the cache map</param>
    /// <returns>Tuple containing entry count and average age in seconds</returns>
    Task<(long entryCount, double averageAgeSeconds)> GetCacheStatisticsAsync(string mapName);

    /// <summary>
    /// Generates a standardized cache key for orchestration workflow data using the pattern:
    /// {orchestratedFlowEntityId}:{correlationId}:{executionId}:{stepId}:{previousStepId}
    /// This ensures consistent key formatting across all processors and orchestration components.
    /// The previousStepId should be Guid.Empty for entry point steps.
    /// </summary>
    string GetCacheKey(Guid orchestratedFlowEntityId, Guid correlationId, Guid executionId, Guid stepId, Guid previousStepId);
}
