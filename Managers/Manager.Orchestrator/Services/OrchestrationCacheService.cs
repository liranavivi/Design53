using System.Diagnostics;
using System.Text.Json;
using Manager.Orchestrator.Models;
using Shared.Correlation;
using Shared.Services;

namespace Manager.Orchestrator.Services;

/// <summary>
/// Orchestration cache service implementation following ProcessorHealthMonitor pattern
/// </summary>
public class OrchestrationCacheService : IOrchestrationCacheService
{
    private readonly ICacheService _cacheService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrchestrationCacheService> _logger;
    private readonly IOrchestratorHealthMetricsService _metricsService;
    private readonly string _mapName;
    private readonly TimeSpan _defaultTtl;
    private readonly int _maxRetries;
    private readonly TimeSpan _retryDelay;
    private readonly JsonSerializerOptions _jsonOptions;

    public OrchestrationCacheService(
        ICacheService cacheService,
        IConfiguration configuration,
        ILogger<OrchestrationCacheService> logger,
        IOrchestratorHealthMetricsService metricsService)
    {
        _cacheService = cacheService;
        _configuration = configuration;
        _logger = logger;
        _metricsService = metricsService;

        _mapName = _configuration["OrchestrationCache:MapName"] ?? "orchestration-data";
        _defaultTtl = TimeSpan.FromMinutes(_configuration.GetValue<int>("OrchestrationCache:DefaultTtlMinutes", 240));
        _maxRetries = _configuration.GetValue<int>("OrchestrationCache:MaxRetries", 3);
        _retryDelay = TimeSpan.FromMilliseconds(_configuration.GetValue<int>("OrchestrationCache:RetryDelayMs", 1000));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task StoreOrchestrationDataAsync(Guid orchestratedFlowId, OrchestrationCacheModel orchestrationData, TimeSpan? ttl = null)
    {
        var cacheKey = orchestratedFlowId.ToString();
        var effectiveTtl = ttl ?? GetDynamicTtl(orchestrationData);
        orchestrationData.ExpiresAt = DateTime.UtcNow.Add(effectiveTtl);

        _logger.LogInformationWithCorrelation("Storing orchestration data in cache. OrchestratedFlowId: {OrchestratedFlowId}, TTL: {TTL}",
            orchestratedFlowId, effectiveTtl);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var cacheValue = JsonSerializer.Serialize(orchestrationData, _jsonOptions);
            await StoreWithRetryAsync(cacheKey, cacheValue, effectiveTtl);
            stopwatch.Stop();

            // Record successful cache operation metrics
            _metricsService.RecordCacheOperation(
                success: true,
                operationType: "store");

            _logger.LogInformationWithCorrelation("Successfully stored orchestration data in cache. OrchestratedFlowId: {OrchestratedFlowId}, StepCount: {StepCount}, AssignmentCount: {AssignmentCount}",
                orchestratedFlowId, orchestrationData.StepManager.StepIds.Count, orchestrationData.AssignmentManager.Assignments.Count);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record failed cache operation metrics
            _metricsService.RecordCacheOperation(
                success: false,
                operationType: "store");

            _logger.LogErrorWithCorrelation(ex, "Failed to store orchestration data in cache. OrchestratedFlowId: {OrchestratedFlowId}",
                orchestratedFlowId);
            throw;
        }
    }

    public async Task<OrchestrationCacheModel?> GetOrchestrationDataAsync(Guid orchestratedFlowId)
    {
        var cacheKey = orchestratedFlowId.ToString();

        _logger.LogDebugWithCorrelation("Retrieving orchestration data from cache. OrchestratedFlowId: {OrchestratedFlowId}",
            orchestratedFlowId);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformationWithCorrelation("Attempting to retrieve orchestration data from cache. OrchestratedFlowId: {OrchestratedFlowId}, MapName: {MapName}, CacheKey: {CacheKey}",
                orchestratedFlowId, _mapName, cacheKey);

            var cacheValue = await _cacheService.GetAsync(_mapName, cacheKey);

            if (string.IsNullOrEmpty(cacheValue))
            {
                stopwatch.Stop();

                // Record cache miss
                _metricsService.RecordCacheOperation(
                    success: false,
                    operationType: "get");

                _logger.LogWarningWithCorrelation("No orchestration data found in cache. OrchestratedFlowId: {OrchestratedFlowId}, MapName: {MapName}, CacheKey: {CacheKey}",
                    orchestratedFlowId, _mapName, cacheKey);
                return null;
            }

            _logger.LogDebugWithCorrelation("Raw cache value retrieved. OrchestratedFlowId: {OrchestratedFlowId}, ValueLength: {ValueLength}",
                orchestratedFlowId, cacheValue.Length);

            var orchestrationData = JsonSerializer.Deserialize<OrchestrationCacheModel>(cacheValue, _jsonOptions);
            stopwatch.Stop();

            if (orchestrationData == null)
            {
                // Record failed deserialization as cache miss
                _metricsService.RecordCacheOperation(
                    success: false,
                    operationType: "get");

                _logger.LogWarningWithCorrelation("Failed to deserialize orchestration data from cache. OrchestratedFlowId: {OrchestratedFlowId}",
                    orchestratedFlowId);
                return null;
            }

            // Check if the entry has expired
            if (orchestrationData.IsExpired)
            {
                // Record expired entry as cache miss
                _metricsService.RecordCacheOperation(
                    success: false,
                    operationType: "get");

                _logger.LogWarningWithCorrelation("Orchestration data in cache has expired. OrchestratedFlowId: {OrchestratedFlowId}, ExpiresAt: {ExpiresAt}",
                    orchestratedFlowId, orchestrationData.ExpiresAt);

                // Remove expired entry
                await RemoveOrchestrationDataAsync(orchestratedFlowId);
                return null;
            }

            // Record successful cache hit
            _metricsService.RecordCacheOperation(
                success: true,
                operationType: "get");

            // Implement sliding expiration - refresh TTL on access for active workflows
            var enableSlidingExpiration = _configuration.GetValue<bool>("OrchestrationCache:EnableSlidingExpiration", true);

            if (enableSlidingExpiration)
            {
                var timeUntilExpiry = orchestrationData.ExpiresAt - DateTime.UtcNow;
                var refreshThresholdMinutes = _configuration.GetValue<int>("OrchestrationCache:RefreshThresholdMinutes", 30);
                var refreshThreshold = TimeSpan.FromMinutes(refreshThresholdMinutes);

                if (timeUntilExpiry < refreshThreshold)
                {
                    _logger.LogInformationWithCorrelation("Refreshing cache TTL for active workflow. OrchestratedFlowId: {OrchestratedFlowId}, TimeLeft: {TimeLeft}, Threshold: {Threshold}",
                        orchestratedFlowId, timeUntilExpiry, refreshThreshold);

                    // Refresh the cache entry with new TTL
                    await StoreOrchestrationDataAsync(orchestratedFlowId, orchestrationData);
                }
            }

            _logger.LogDebugWithCorrelation("Successfully retrieved orchestration data from cache. OrchestratedFlowId: {OrchestratedFlowId}, StepCount: {StepCount}, AssignmentCount: {AssignmentCount}",
                orchestratedFlowId, orchestrationData.StepManager.StepIds.Count, orchestrationData.AssignmentManager.Assignments.Count);

            return orchestrationData;
        }
        catch (Exception ex)
        {
            // Record failed cache operation
            _metricsService.RecordCacheOperation(
                success: false,
                operationType: "get");

            _logger.LogErrorWithCorrelation(ex, "Error retrieving orchestration data from cache. OrchestratedFlowId: {OrchestratedFlowId}",
                orchestratedFlowId);
            return null;
        }
    }

    public async Task RemoveOrchestrationDataAsync(Guid orchestratedFlowId)
    {
        var cacheKey = orchestratedFlowId.ToString();

        _logger.LogInformationWithCorrelation("Removing orchestration data from cache. OrchestratedFlowId: {OrchestratedFlowId}",
            orchestratedFlowId);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _cacheService.RemoveAsync(_mapName, cacheKey);
            stopwatch.Stop();

            // Record successful cache remove operation
            _metricsService.RecordCacheOperation(
                success: true,
                operationType: "remove");

            _logger.LogInformationWithCorrelation("Successfully removed orchestration data from cache. OrchestratedFlowId: {OrchestratedFlowId}",
                orchestratedFlowId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record failed cache remove operation
            _metricsService.RecordCacheOperation(
                success: false,
                operationType: "remove");

            _logger.LogErrorWithCorrelation(ex, "Error removing orchestration data from cache. OrchestratedFlowId: {OrchestratedFlowId}",
                orchestratedFlowId);
            throw;
        }
    }

    public async Task<bool> ExistsAndValidAsync(Guid orchestratedFlowId)
    {
        _logger.LogDebugWithCorrelation("Checking if orchestration data exists and is valid. OrchestratedFlowId: {OrchestratedFlowId}",
            orchestratedFlowId);

        try
        {
            var orchestrationData = await GetOrchestrationDataAsync(orchestratedFlowId);
            var exists = orchestrationData != null && !orchestrationData.IsExpired;

            _logger.LogDebugWithCorrelation("Orchestration data existence check result. OrchestratedFlowId: {OrchestratedFlowId}, Exists: {Exists}",
                orchestratedFlowId, exists);

            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error checking orchestration data existence. OrchestratedFlowId: {OrchestratedFlowId}",
                orchestratedFlowId);
            return false;
        }
    }

    private async Task StoreWithRetryAsync(string cacheKey, string cacheValue, TimeSpan ttl)
    {
        var retryCount = 0;

        while (retryCount <= _maxRetries)
        {
            try
            {
                await _cacheService.SetAsync(_mapName, cacheKey, cacheValue, ttl);
                _logger.LogDebugWithCorrelation("Successfully stored cache entry. Key: {CacheKey}", cacheKey);
                return; // Success
            }
            catch (Exception ex)
            {
                retryCount++;

                if (retryCount > _maxRetries)
                {
                    _logger.LogErrorWithCorrelation(ex, "Failed to store cache entry after {MaxRetries} retries. Key: {CacheKey}",
                        _maxRetries, cacheKey);
                    throw;
                }

                var delay = TimeSpan.FromMilliseconds(_retryDelay.TotalMilliseconds * Math.Pow(2, retryCount - 1));

                _logger.LogWarningWithCorrelation(ex, "Failed to store cache entry, retry {RetryCount}/{MaxRetries} in {Delay}ms. Key: {CacheKey}",
                    retryCount, _maxRetries, delay.TotalMilliseconds, cacheKey);

                await Task.Delay(delay);
            }
        }
    }

    private TimeSpan GetDynamicTtl(OrchestrationCacheModel orchestrationData)
    {
        // For endless scheduling workflows, use very long TTL
        var isEndlessWorkflow = _configuration.GetValue<bool>("OrchestrationCache:EnableEndlessWorkflows", false);

        if (isEndlessWorkflow)
        {
            // Use 30 days for endless workflows
            var endlessTtlDays = _configuration.GetValue<int>("OrchestrationCache:EndlessWorkflowTtlDays", 30);
            return TimeSpan.FromDays(endlessTtlDays);
        }

        return _defaultTtl;
    }
}
