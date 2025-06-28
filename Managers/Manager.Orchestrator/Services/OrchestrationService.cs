using System.Diagnostics;
using System.Text.Json;
using Manager.Orchestrator.Models;
using MassTransit;
using Shared.Correlation;
using Shared.Extensions;
using Shared.MassTransit.Commands;
using Shared.Models;
using Shared.Services;

namespace Manager.Orchestrator.Services;

/// <summary>
/// Main orchestration business logic service
/// </summary>
public class OrchestrationService : IOrchestrationService
{
    private readonly IManagerHttpClient _managerHttpClient;
    private readonly IOrchestrationCacheService _cacheService;
    private readonly ICacheService _rawCacheService;
    private readonly IOrchestrationSchedulerService _schedulerService;
    private readonly ILogger<OrchestrationService> _logger;
    private readonly IBus _bus;
    private readonly IOrchestratorHealthMetricsService _metricsService;
    private static readonly ActivitySource ActivitySource = new("Manager.Orchestrator.Services");

    public OrchestrationService(
        IManagerHttpClient managerHttpClient,
        IOrchestrationCacheService cacheService,
        ICacheService rawCacheService,
        IOrchestrationSchedulerService schedulerService,
        ILogger<OrchestrationService> logger,
        IBus bus,
        IOrchestratorHealthMetricsService metricsService)
    {
        _managerHttpClient = managerHttpClient;
        _cacheService = cacheService;
        _rawCacheService = rawCacheService;
        _schedulerService = schedulerService;
        _logger = logger;
        _bus = bus;
        _metricsService = metricsService;
    }

    public async Task StartOrchestrationAsync(Guid orchestratedFlowId)
    {
        await StopOrchestrationAsync(orchestratedFlowId);


        // ✅ Try to get correlation ID from current context, generate new one if this is truly a new workflow start
        var correlationId = GetCurrentCorrelationIdOrGenerate();
        var initiatedBy = "System"; // TODO: Get from user context

        using var activity = ActivitySource.StartActivityWithCorrelation("StartOrchestration");
        activity?.SetTag("orchestratedFlowId", orchestratedFlowId.ToString())
                ?.SetTag("correlationId", correlationId.ToString())
                ?.SetTag("initiatedBy", initiatedBy)
                ?.SetTag("operation", "StartOrchestration");

        var stopwatch = Stopwatch.StartNew();
        var publishedCommands = 0;
        var stepCount = 0;
        var assignmentCount = 0;
        var processorCount = 0;
        var entryPointCount = 0;

        _logger.LogInformationWithCorrelation("Starting orchestration. OrchestratedFlowId: {OrchestratedFlowId}, InitiatedBy: {InitiatedBy}",
            orchestratedFlowId, initiatedBy);

        try
        {
            // Check if orchestration is already active
            activity?.SetTag("step", "0-CheckExisting");
            if (await _cacheService.ExistsAndValidAsync(orchestratedFlowId))
            {
                activity?.SetTag("result", "AlreadyActive");
                _logger.LogInformationWithCorrelation("Orchestration already active. OrchestratedFlowId: {OrchestratedFlowId}",
                    orchestratedFlowId);
                return;
            }

            // Step 1: Retrieve orchestrated flow entity
            activity?.SetTag("step", "1-RetrieveOrchestratedFlow");
            var orchestratedFlow = await _managerHttpClient.GetOrchestratedFlowAsync(orchestratedFlowId);
            if (orchestratedFlow == null)
            {
                throw new InvalidOperationException($"Orchestrated flow not found: {orchestratedFlowId}");
            }

            activity?.SetTag("workflowId", orchestratedFlow.WorkflowId.ToString())
                    ?.SetTag("assignmentIdCount", orchestratedFlow.AssignmentIds.Count);

            _logger.LogInformationWithCorrelation("Retrieved orchestrated flow. OrchestratedFlowId: {OrchestratedFlowId}, WorkflowId: {WorkflowId}, AssignmentCount: {AssignmentCount}",
                orchestratedFlowId, orchestratedFlow.WorkflowId, orchestratedFlow.AssignmentIds.Count);

            // Step 2: Gather all data from managers in parallel
            activity?.SetTag("step", "2-GatherManagerData");
            var stepManagerTask = _managerHttpClient.GetStepManagerDataAsync(orchestratedFlow.WorkflowId);
            var assignmentManagerTask = _managerHttpClient.GetAssignmentManagerDataAsync(orchestratedFlow.AssignmentIds);

            await Task.WhenAll(stepManagerTask, assignmentManagerTask);

            var stepManagerData = await stepManagerTask;
            var assignmentManagerData = await assignmentManagerTask;

            // Collect metrics
            stepCount = stepManagerData.StepIds.Count;
            assignmentCount = assignmentManagerData.Assignments.Values.Sum(list => list.Count);
            processorCount = stepManagerData.ProcessorIds.Count;

            activity?.SetTag("stepCount", stepCount)
                    ?.SetTag("assignmentCount", assignmentCount)
                    ?.SetTag("processorCount", processorCount);

            // Step 3: Find and validate entry points
            activity?.SetTag("step", "3-FindAndValidateEntryPoints");
            var entryPoints = FindEntryPoints(stepManagerData);
            ValidateEntryPoints(entryPoints);
            entryPointCount = entryPoints.Count;
            activity?.SetTag("entryPointCount", entryPointCount);

            // Step 3.5: Find and validate termination points
            activity?.SetTag("step", "3.5-FindAndValidateTerminationPoints");
            var terminationPoints = FindTerminationPoints(stepManagerData);
            ValidateTerminationPoints(terminationPoints);
            var terminationPointCount = terminationPoints.Count;
            activity?.SetTag("terminationPointCount", terminationPointCount);

            // Step 4: Create complete orchestration cache model
            activity?.SetTag("step", "4-CreateCacheModel");
            var orchestrationData = new OrchestrationCacheModel
            {
                OrchestratedFlowId = orchestratedFlowId,
                OrchestratedFlow = orchestratedFlow,
                StepManager = stepManagerData,
                AssignmentManager = assignmentManagerData,
                EntryPoints = entryPoints, // ✅ Cache the calculated entry points
                CreatedAt = DateTime.UtcNow
            };

            // Step 5: Store in cache
            activity?.SetTag("step", "5-StoreInCache");
            await _cacheService.StoreOrchestrationDataAsync(orchestratedFlowId, orchestrationData);

            // Step 6: Check processor health
            activity?.SetTag("step", "6-ValidateProcessorHealth");
            await ValidateProcessorHealthAsync(stepManagerData.ProcessorIds);

            publishedCommands = orchestrationData.EntryPoints.Count;

            // Step 7: Start scheduler if cron expression is provided and enabled
            activity?.SetTag("step", "7-StartScheduler");
            await StartSchedulerIfConfiguredAsync(orchestratedFlowId, orchestratedFlow);

            stopwatch.Stop();
            activity?.SetTag("publishedCommands", publishedCommands)
                    ?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds)
                    ?.SetTag("result", "Success")
                    ?.SetStatus(ActivityStatusCode.Ok);


            _logger.LogInformationWithCorrelation("Successfully started orchestration. OrchestratedFlowId: {OrchestratedFlowId}, StepCount: {StepCount}, AssignmentCount: {AssignmentCount}, ProcessorCount: {ProcessorCount}, EntryPoints: {EntryPointCount}, PublishedCommands: {PublishedCommands}, Duration: {Duration}ms",
                orchestratedFlowId, stepCount, assignmentCount, processorCount, entryPointCount, publishedCommands, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message)
                    ?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds)
                    ?.SetTag("error.type", ex.GetType().Name)
                    ?.SetTag("result", "Error");

            // Record orchestration start exception as critical
            _metricsService.RecordException(ex.GetType().Name, "error", isCritical: true);

            _logger.LogErrorWithCorrelation(ex, "Error starting orchestration. OrchestratedFlowId: {OrchestratedFlowId}, Duration: {Duration}ms, ErrorType: {ErrorType}",
                orchestratedFlowId, stopwatch.ElapsedMilliseconds, ex.GetType().Name);
            throw;
        }
    }

    public async Task StopOrchestrationAsync(Guid orchestratedFlowId)
    {
        _logger.LogInformationWithCorrelation("Stopping orchestration. OrchestratedFlowId: {OrchestratedFlowId}",
            orchestratedFlowId);

        try
        {
            // Check if orchestration exists
            var exists = await _cacheService.ExistsAndValidAsync(orchestratedFlowId);
            if (!exists)
            {
                _logger.LogWarningWithCorrelation("Orchestration not found or already expired. OrchestratedFlowId: {OrchestratedFlowId}",
                    orchestratedFlowId);

                // Still try to stop scheduler in case it's running
                await StopSchedulerIfRunningAsync(orchestratedFlowId);
                return;
            }

            // Stop scheduler if running
            await StopSchedulerIfRunningAsync(orchestratedFlowId);

            // Remove from cache
            await _cacheService.RemoveOrchestrationDataAsync(orchestratedFlowId);

            _logger.LogInformationWithCorrelation("Successfully stopped orchestration. OrchestratedFlowId: {OrchestratedFlowId}",
                orchestratedFlowId);
        }
        catch (Exception ex)
        {
            // Record orchestration stop exception as critical
            _metricsService.RecordException(ex.GetType().Name, "error", isCritical: true);

            _logger.LogErrorWithCorrelation(ex, "Failed to stop orchestration. OrchestratedFlowId: {OrchestratedFlowId}",
                orchestratedFlowId);
            throw;
        }
    }

    public async Task<OrchestrationStatusModel> GetOrchestrationStatusAsync(Guid orchestratedFlowId)
    {
        _logger.LogDebugWithCorrelation("Getting orchestration status. OrchestratedFlowId: {OrchestratedFlowId}",
            orchestratedFlowId);

        try
        {
            var orchestrationData = await _cacheService.GetOrchestrationDataAsync(orchestratedFlowId);

            if (orchestrationData == null)
            {
                _logger.LogDebugWithCorrelation("Orchestration not found in cache. OrchestratedFlowId: {OrchestratedFlowId}",
                    orchestratedFlowId);

                return new OrchestrationStatusModel
                {
                    OrchestratedFlowId = orchestratedFlowId,
                    IsActive = false,
                    StartedAt = null,
                    ExpiresAt = null,
                    StepCount = 0,
                    AssignmentCount = 0
                };
            }

            var totalAssignments = orchestrationData.AssignmentManager.Assignments.Values.Sum(list => list.Count);

            var status = new OrchestrationStatusModel
            {
                OrchestratedFlowId = orchestratedFlowId,
                IsActive = true,
                StartedAt = orchestrationData.CreatedAt,
                ExpiresAt = orchestrationData.ExpiresAt,
                StepCount = orchestrationData.StepManager.StepIds.Count,
                AssignmentCount = totalAssignments
            };

            _logger.LogDebugWithCorrelation("Retrieved orchestration status. OrchestratedFlowId: {OrchestratedFlowId}, IsActive: {IsActive}, StepCount: {StepCount}, AssignmentCount: {AssignmentCount}",
                orchestratedFlowId, status.IsActive, status.StepCount, status.AssignmentCount);

            return status;
        }
        catch (Exception ex)
        {
            // Record orchestration status retrieval exception as non-critical
            _metricsService.RecordException(ex.GetType().Name, "error", isCritical: false);

            _logger.LogErrorWithCorrelation(ex, "Error getting orchestration status. OrchestratedFlowId: {OrchestratedFlowId}",
                orchestratedFlowId);
            throw;
        }
    }

    /// <summary>
    /// Validates the health of all processors required for orchestration
    /// </summary>
    /// <param name="processorIds">Collection of processor IDs to validate</param>
    /// <exception cref="InvalidOperationException">Thrown when one or more processors are unhealthy</exception>
    private async Task ValidateProcessorHealthAsync(List<Guid> processorIds)
    {
        _logger.LogDebugWithCorrelation("Validating health of {ProcessorCount} processors", processorIds.Count);

        var unhealthyProcessors = new List<(Guid ProcessorId, string Reason)>();

        foreach (var processorId in processorIds)
        {
            try
            {
                // Get processor health from cache using the same map name as Processor.File
                var healthData = await _rawCacheService.GetAsync("processor-health", processorId.ToString());

                if (string.IsNullOrEmpty(healthData))
                {
                    unhealthyProcessors.Add((processorId, "No health data found in cache"));
                    _logger.LogWarningWithCorrelation("No health data found for processor {ProcessorId}", processorId);
                    continue;
                }

                // Deserialize health entry
                var healthEntry = System.Text.Json.JsonSerializer.Deserialize<ProcessorHealthCacheEntry>(healthData);

                if (healthEntry == null)
                {
                    unhealthyProcessors.Add((processorId, "Failed to deserialize health data"));
                    _logger.LogWarningWithCorrelation("Failed to deserialize health data for processor {ProcessorId}", processorId);
                    continue;
                }

                // Check if health entry has expired
                if (healthEntry.IsExpired)
                {
                    unhealthyProcessors.Add((processorId, $"Health data expired at {healthEntry.ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC"));
                    _logger.LogWarningWithCorrelation("Health data expired for processor {ProcessorId}. ExpiresAt: {ExpiresAt}",
                        processorId, healthEntry.ExpiresAt);
                    continue;
                }

                // Check if health data is still valid based on health check interval
                var nowUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var timeSinceLastUpdate = nowUnixTime - healthEntry.LastUpdated;
                var healthCheckIntervalWithBuffer = healthEntry.HealthCheckInterval * 2; // Allow 2x interval as buffer

                if (timeSinceLastUpdate > healthCheckIntervalWithBuffer)
                {
                    unhealthyProcessors.Add((processorId, $"Health data is stale. Last updated {timeSinceLastUpdate}s ago, threshold: {healthCheckIntervalWithBuffer}s"));
                    _logger.LogWarningWithCorrelation("Health data for processor {ProcessorId} is stale. LastUpdated: {LastUpdated}, " +
                        "TimeSinceLastUpdate: {TimeSinceLastUpdate}s, HealthCheckInterval: {HealthCheckInterval}s, BufferThreshold: {BufferThreshold}s",
                        processorId, healthEntry.LastUpdated, timeSinceLastUpdate, healthEntry.HealthCheckInterval, healthCheckIntervalWithBuffer);
                    continue;
                }

                // Check processor health status
                if (healthEntry.Status != HealthStatus.Healthy)
                {
                    unhealthyProcessors.Add((processorId, $"Processor status: {healthEntry.Status}, Message: {healthEntry.Message}"));
                    _logger.LogWarningWithCorrelation("Processor {ProcessorId} is not healthy. Status: {Status}, Message: {Message}",
                        processorId, healthEntry.Status, healthEntry.Message);
                    continue;
                }

                _logger.LogDebugWithCorrelation("Processor {ProcessorId} is healthy. Status: {Status}, LastUpdated: {LastUpdated}, " +
                    "HealthCheckInterval: {HealthCheckInterval}s, TimeSinceLastUpdate: {TimeSinceLastUpdate}s, IsValid: true",
                    processorId, healthEntry.Status, healthEntry.LastUpdated, healthEntry.HealthCheckInterval, timeSinceLastUpdate);
            }
            catch (Exception ex)
            {
                // Record processor health check exception as non-critical
                _metricsService.RecordException(ex.GetType().Name, "warning", isCritical: false);

                unhealthyProcessors.Add((processorId, $"Error checking health: {ex.Message}"));
                _logger.LogErrorWithCorrelation(ex, "Error checking health for processor {ProcessorId}", processorId);
            }
        }

        // If any processors are unhealthy, fail the orchestration
        if (unhealthyProcessors.Count > 0)
        {
            var errorMessage = $"Failed to start orchestration: {unhealthyProcessors.Count} of {processorIds.Count} processors are unhealthy. " +
                              $"Unhealthy processors: {string.Join(", ", unhealthyProcessors.Select(p => $"{p.ProcessorId} ({p.Reason})"))}";

            _logger.LogErrorWithCorrelation("Processor health validation failed. {ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        _logger.LogInformationWithCorrelation("All {ProcessorCount} processors are healthy", processorIds.Count);
    }

    public async Task<bool> ValidateProcessorHealthForExecutionAsync(List<Guid> processorIds)
    {
        try
        {
            await ValidateProcessorHealthAsync(processorIds);
            return true;
        }
        catch (InvalidOperationException)
        {
            // Health validation failed
            return false;
        }
    }

    public async Task<ProcessorHealthResponse?> GetProcessorHealthAsync(Guid processorId)
    {
        using var activity = ActivitySource.StartActivity("GetProcessorHealth");
        activity?.SetTag("processor.id", processorId.ToString());

        _logger.LogDebugWithCorrelation("Getting health status for processor {ProcessorId}", processorId);

        try
        {
            // Get processor health from cache using the same map name as ProcessorHealthMonitor
            var healthData = await _rawCacheService.GetAsync("processor-health", processorId.ToString());

            if (string.IsNullOrEmpty(healthData))
            {
                _logger.LogDebugWithCorrelation("No health data found for processor {ProcessorId}", processorId);
                return null;
            }

            var healthEntry = JsonSerializer.Deserialize<ProcessorHealthCacheEntry>(healthData);
            if (healthEntry == null)
            {
                _logger.LogWarningWithCorrelation("Failed to deserialize health data for processor {ProcessorId}", processorId);
                return null;
            }

            // Check if health data is expired
            if (healthEntry.IsExpired)
            {
                _logger.LogDebugWithCorrelation("Health data for processor {ProcessorId} is expired. ExpiresAt: {ExpiresAt}",
                    processorId, healthEntry.ExpiresAt);
                return null;
            }

            // Check if health data is still valid based on health check interval
            var nowUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timeSinceLastUpdate = nowUnixTime - healthEntry.LastUpdated;
            var healthCheckIntervalWithBuffer = healthEntry.HealthCheckInterval * 2; // Allow 2x interval as buffer

            if (timeSinceLastUpdate > healthCheckIntervalWithBuffer)
            {
                _logger.LogWarningWithCorrelation("Health data for processor {ProcessorId} is stale. LastUpdated: {LastUpdated}, " +
                    "TimeSinceLastUpdate: {TimeSinceLastUpdate}s, HealthCheckInterval: {HealthCheckInterval}s, BufferThreshold: {BufferThreshold}s",
                    processorId, healthEntry.LastUpdated, timeSinceLastUpdate, healthEntry.HealthCheckInterval, healthCheckIntervalWithBuffer);
                return null;
            }

            var response = new Shared.Models.ProcessorHealthResponse
            {
                CorrelationId = healthEntry.CorrelationId,
                HealthCheckId = healthEntry.HealthCheckId,
                ProcessorId = healthEntry.ProcessorId,
                Status = healthEntry.Status,
                Message = healthEntry.Message,
                LastUpdated = healthEntry.LastUpdated,
                HealthCheckInterval = healthEntry.HealthCheckInterval,
                ExpiresAt = healthEntry.ExpiresAt,
                ReportingPodId = healthEntry.ReportingPodId,
                Uptime = healthEntry.Uptime,
                Metadata = healthEntry.Metadata,
                PerformanceMetrics = healthEntry.PerformanceMetrics,
                HealthChecks = healthEntry.HealthChecks,
                RetrievedAt = DateTime.UtcNow
            };

            _logger.LogDebugWithCorrelation("Successfully retrieved health status for processor {ProcessorId}. Status: {Status}, LastUpdated: {LastUpdated}, " +
                "HealthCheckInterval: {HealthCheckInterval}s, TimeSinceLastUpdate: {TimeSinceLastUpdate}s, IsValid: true",
                processorId, healthEntry.Status, healthEntry.LastUpdated, healthEntry.HealthCheckInterval, timeSinceLastUpdate);

            return response;
        }
        catch (Exception ex)
        {
            activity?.SetErrorTags(ex);
            _logger.LogErrorWithCorrelation(ex, "Error getting health status for processor {ProcessorId}", processorId);
            throw;
        }
    }

    public async Task<ProcessorsHealthResponse?> GetProcessorsHealthByOrchestratedFlowAsync(Guid orchestratedFlowId)
    {
        using var activity = ActivitySource.StartActivity("GetProcessorsHealthByOrchestratedFlow");
        activity?.SetTag("orchestrated_flow.id", orchestratedFlowId.ToString());

        _logger.LogDebugWithCorrelation("Getting processors health for orchestrated flow {OrchestratedFlowId}", orchestratedFlowId);

        try
        {
            // Get orchestration data from cache to retrieve processor IDs
            var orchestrationData = await _cacheService.GetOrchestrationDataAsync(orchestratedFlowId);

            if (orchestrationData == null)
            {
                _logger.LogDebugWithCorrelation("Orchestrated flow {OrchestratedFlowId} not found in cache", orchestratedFlowId);
                return null;
            }

            if (orchestrationData.IsExpired)
            {
                _logger.LogDebugWithCorrelation("Orchestration data for {OrchestratedFlowId} is expired. ExpiresAt: {ExpiresAt}",
                    orchestratedFlowId, orchestrationData.ExpiresAt);
                return null;
            }

            var processorIds = orchestrationData.StepManager.ProcessorIds;
            _logger.LogDebugWithCorrelation("Found {ProcessorCount} processors for orchestrated flow {OrchestratedFlowId}",
                processorIds.Count, orchestratedFlowId);

            var processorsHealth = new Dictionary<Guid, Shared.Models.ProcessorHealthResponse>();
            var summary = new Shared.Models.ProcessorsHealthSummary
            {
                TotalProcessors = processorIds.Count
            };

            // Get health status for each processor
            foreach (var processorId in processorIds)
            {
                try
                {
                    var healthResponse = await GetProcessorHealthAsync(processorId);

                    if (healthResponse != null)
                    {
                        processorsHealth[processorId] = healthResponse;

                        // Update summary counts
                        switch (healthResponse.Status)
                        {
                            case HealthStatus.Healthy:
                                summary.HealthyProcessors++;
                                break;
                            case HealthStatus.Degraded:
                                summary.DegradedProcessors++;
                                summary.ProblematicProcessors.Add(processorId);
                                break;
                            case HealthStatus.Unhealthy:
                                summary.UnhealthyProcessors++;
                                summary.ProblematicProcessors.Add(processorId);
                                break;
                        }
                    }
                    else
                    {
                        summary.NoHealthDataProcessors++;
                        summary.ProblematicProcessors.Add(processorId);
                        _logger.LogWarningWithCorrelation("No health data found for processor {ProcessorId} in orchestrated flow {OrchestratedFlowId}",
                            processorId, orchestratedFlowId);
                    }
                }
                catch (Exception ex)
                {
                    summary.NoHealthDataProcessors++;
                    summary.ProblematicProcessors.Add(processorId);
                    _logger.LogErrorWithCorrelation(ex, "Error getting health for processor {ProcessorId} in orchestrated flow {OrchestratedFlowId}",
                        processorId, orchestratedFlowId);
                }
            }

            // Determine overall status
            if (summary.UnhealthyProcessors > 0 || summary.NoHealthDataProcessors > 0)
            {
                summary.OverallStatus = HealthStatus.Unhealthy;
            }
            else if (summary.DegradedProcessors > 0)
            {
                summary.OverallStatus = HealthStatus.Degraded;
            }
            else
            {
                summary.OverallStatus = HealthStatus.Healthy;
            }

            var response = new Shared.Models.ProcessorsHealthResponse
            {
                OrchestratedFlowId = orchestratedFlowId,
                Processors = processorsHealth,
                Summary = summary,
                RetrievedAt = DateTime.UtcNow
            };

            _logger.LogInformationWithCorrelation("Successfully retrieved health status for {ProcessorCount} processors in orchestrated flow {OrchestratedFlowId}. " +
                                 "Healthy: {HealthyCount}, Degraded: {DegradedCount}, Unhealthy: {UnhealthyCount}, NoData: {NoDataCount}, Overall: {OverallStatus}",
                processorIds.Count, orchestratedFlowId, summary.HealthyProcessors, summary.DegradedProcessors,
                summary.UnhealthyProcessors, summary.NoHealthDataProcessors, summary.OverallStatus);

            return response;
        }
        catch (Exception ex)
        {
            activity?.SetErrorTags(ex);
            _logger.LogErrorWithCorrelation(ex, "Error getting processors health for orchestrated flow {OrchestratedFlowId}", orchestratedFlowId);
            throw;
        }
    }

    /// <summary>
    /// Finds entry points in the workflow by identifying steps that are not referenced as next steps
    /// </summary>
    /// <param name="stepManagerData">Step manager data containing step and next step information</param>
    /// <returns>Collection of step IDs that are entry points</returns>
    private List<Guid> FindEntryPoints(StepManagerModel stepManagerData)
    {
        _logger.LogDebugWithCorrelation("Finding entry points from {StepCount} steps with {NextStepCount} next step references",
            stepManagerData.StepIds.Count, stepManagerData.NextStepIds.Count);

        var entryPoints = new List<Guid>();

        // Find steps that are not referenced as next steps (entry points)
        foreach (var stepId in stepManagerData.StepIds)
        {
            if (!stepManagerData.NextStepIds.Contains(stepId))
            {
                entryPoints.Add(stepId);
                _logger.LogDebugWithCorrelation("Found entry point: {StepId}", stepId);
            }
        }

        _logger.LogInformationWithCorrelation("Found {EntryPointCount} entry points in workflow", entryPoints.Count);
        return entryPoints;
    }

    /// <summary>
    /// Validates that the workflow has valid entry points and is not cyclical
    /// </summary>
    /// <param name="entryPoints">Collection of entry point step IDs</param>
    /// <exception cref="InvalidOperationException">Thrown when workflow has no entry points (indicating cyclicity)</exception>
    private void ValidateEntryPoints(List<Guid> entryPoints)
    {
        _logger.LogDebugWithCorrelation("Validating {EntryPointCount} entry points", entryPoints.Count);

        if (entryPoints.Count == 0)
        {
            var errorMessage = "Failed to start orchestration: No entry points found in workflow. " +
                              "This indicates that the flow is cyclical, which is not allowed. " +
                              "Every workflow must have at least one step that is not referenced as a next step.";

            _logger.LogErrorWithCorrelation("Entry point validation failed: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        _logger.LogInformationWithCorrelation("Entry point validation passed. Found {EntryPointCount} valid entry points: {EntryPoints}",
            entryPoints.Count, string.Join(", ", entryPoints));
    }

    /// <summary>
    /// Finds termination points in the workflow by identifying steps that have no next steps
    /// </summary>
    /// <param name="stepManagerData">Step manager data containing step entities</param>
    /// <returns>Collection of step IDs that are termination points</returns>
    private List<Guid> FindTerminationPoints(StepManagerModel stepManagerData)
    {
        _logger.LogDebugWithCorrelation("Finding termination points from {StepCount} steps",
            stepManagerData.StepIds.Count);

        var terminationPoints = new List<Guid>();

        // Find steps that have no next steps (termination points)
        foreach (var stepEntity in stepManagerData.StepEntities.Values)
        {
            if (stepEntity.NextStepIds == null || stepEntity.NextStepIds.Count == 0)
            {
                terminationPoints.Add(stepEntity.Id);
                _logger.LogDebugWithCorrelation("Found termination point: {StepId}", stepEntity.Id);
            }
        }

        _logger.LogInformationWithCorrelation("Found {TerminationPointCount} termination points in workflow", terminationPoints.Count);
        return terminationPoints;
    }

    /// <summary>
    /// Validates that the workflow has valid termination points
    /// </summary>
    /// <param name="terminationPoints">Collection of termination point step IDs</param>
    /// <exception cref="InvalidOperationException">Thrown when workflow has no termination points (indicating endless loop)</exception>
    private void ValidateTerminationPoints(List<Guid> terminationPoints)
    {
        _logger.LogDebugWithCorrelation("Validating {TerminationPointCount} termination points", terminationPoints.Count);

        if (terminationPoints.Count == 0)
        {
            var errorMessage = "Failed to start orchestration: No termination points found in workflow. " +
                              "This indicates that the flow has no end points, which could lead to endless execution. " +
                              "Every workflow must have at least one step that has no next steps.";

            _logger.LogErrorWithCorrelation("Termination point validation failed: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        _logger.LogInformationWithCorrelation("Termination point validation passed. Found {TerminationPointCount} valid termination points: {TerminationPoints}",
            terminationPoints.Count, string.Join(", ", terminationPoints));
    }

    /// <summary>
    /// Gets correlation ID from current context or generates a new one if none exists.
    /// This is appropriate for workflow start operations.
    /// </summary>
    private Guid GetCurrentCorrelationIdOrGenerate()
    {
        // Try to get from Activity baggage first (from HTTP request context)
        var activity = Activity.Current;
        if (activity?.GetBaggageItem("correlation.id") is string baggageValue &&
            Guid.TryParse(baggageValue, out var correlationId))
        {
            return correlationId;
        }

        // Generate new correlation ID for new workflow start
        return Guid.NewGuid();
    }

    /// <summary>
    /// Starts the scheduler for the orchestrated flow if cron expression is configured and enabled
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID</param>
    /// <param name="orchestratedFlow">The orchestrated flow entity</param>
    private async Task StartSchedulerIfConfiguredAsync(Guid orchestratedFlowId, Shared.Entities.OrchestratedFlowEntity orchestratedFlow)
    {
        try
        {
            // Check if scheduling is configured and enabled
            if (string.IsNullOrWhiteSpace(orchestratedFlow.CronExpression) || !orchestratedFlow.IsScheduleEnabled)
            {
                _logger.LogDebugWithCorrelation("Scheduler not configured or disabled for orchestrated flow {OrchestratedFlowId}. CronExpression: {CronExpression}, IsScheduleEnabled: {IsScheduleEnabled}",
                    orchestratedFlowId, orchestratedFlow.CronExpression ?? "null", orchestratedFlow.IsScheduleEnabled);
                return;
            }

            // Validate cron expression
            if (!_schedulerService.ValidateCronExpression(orchestratedFlow.CronExpression))
            {
                _logger.LogWarningWithCorrelation("Invalid cron expression for orchestrated flow {OrchestratedFlowId}: {CronExpression}. Skipping scheduler start.",
                    orchestratedFlowId, orchestratedFlow.CronExpression);
                return;
            }

            // Log one-time execution information
            if (orchestratedFlow.IsOneTimeExecution)
            {
                _logger.LogInformationWithCorrelation(
                    "Starting one-time execution scheduler for orchestrated flow {OrchestratedFlowId} with cron expression: {CronExpression}. Job will stop after first execution.",
                    orchestratedFlowId, orchestratedFlow.CronExpression);
            }

            // Check if scheduler is already running
            var isRunning = await _schedulerService.IsSchedulerRunningAsync(orchestratedFlowId);
            if (isRunning)
            {
                _logger.LogInformationWithCorrelation("Scheduler already running for orchestrated flow {OrchestratedFlowId}. Updating with new cron expression: {CronExpression}",
                    orchestratedFlowId, orchestratedFlow.CronExpression);

                // Update existing scheduler with new cron expression
                await _schedulerService.UpdateSchedulerAsync(orchestratedFlowId, orchestratedFlow.CronExpression);
            }
            else
            {
                _logger.LogInformationWithCorrelation("Starting scheduler for orchestrated flow {OrchestratedFlowId} with cron expression: {CronExpression}",
                    orchestratedFlowId, orchestratedFlow.CronExpression);

                // Start new scheduler
                await _schedulerService.StartSchedulerAsync(orchestratedFlowId, orchestratedFlow.CronExpression);
            }

            // Get next execution time for logging
            var nextExecution = await _schedulerService.GetNextExecutionTimeAsync(orchestratedFlowId);
            _logger.LogInformationWithCorrelation("Scheduler configured for orchestrated flow {OrchestratedFlowId}. Next execution: {NextExecution}",
                orchestratedFlowId, nextExecution?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "Unknown");
        }
        catch (Exception ex)
        {
            // Record scheduler start exception as non-critical (doesn't prevent orchestration)
            _metricsService.RecordException(ex.GetType().Name, "warning", isCritical: false);

            _logger.LogErrorWithCorrelation(ex, "Failed to start scheduler for orchestrated flow {OrchestratedFlowId}. CronExpression: {CronExpression}",
                orchestratedFlowId, orchestratedFlow.CronExpression);

            // Don't throw - scheduler failure shouldn't prevent orchestration start
            // The orchestration can still be executed manually
        }
    }

    /// <summary>
    /// Stops the scheduler for the orchestrated flow if it's running
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID</param>
    private async Task StopSchedulerIfRunningAsync(Guid orchestratedFlowId)
    {
        try
        {
            // Check if scheduler is running
            var isRunning = await _schedulerService.IsSchedulerRunningAsync(orchestratedFlowId);
            if (!isRunning)
            {
                _logger.LogDebugWithCorrelation("No scheduler running for orchestrated flow {OrchestratedFlowId}",
                    orchestratedFlowId);
                return;
            }

            _logger.LogInformationWithCorrelation("Stopping scheduler for orchestrated flow {OrchestratedFlowId}",
                orchestratedFlowId);

            // Stop the scheduler
            await _schedulerService.StopSchedulerAsync(orchestratedFlowId);

            _logger.LogInformationWithCorrelation("Successfully stopped scheduler for orchestrated flow {OrchestratedFlowId}",
                orchestratedFlowId);
        }
        catch (Exception ex)
        {
            // Record scheduler stop exception as non-critical (doesn't prevent orchestration stop)
            _metricsService.RecordException(ex.GetType().Name, "warning", isCritical: false);

            _logger.LogErrorWithCorrelation(ex, "Failed to stop scheduler for orchestrated flow {OrchestratedFlowId}",
                orchestratedFlowId);

            // Don't throw - scheduler failure shouldn't prevent orchestration stop
            // The cache cleanup is more important
        }
    }
}
