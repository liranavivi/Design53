using System.Diagnostics;
using Manager.Orchestrator.Models;
using Manager.Orchestrator.Services;
using MassTransit;
using Shared.Correlation;
using Shared.Extensions;
using Shared.Entities.Enums;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;
using Shared.Models;
using Shared.Services;

namespace Manager.Orchestrator.Consumers;

/// <summary>
/// Consumer for ActivityExecutedEvent that handles workflow progression
/// Manages the transition from completed steps to their next steps
/// </summary>
public class ActivityExecutedEventConsumer : IConsumer<ActivityExecutedEvent>
{
    private readonly IOrchestrationCacheService _orchestrationCacheService;
    private readonly ICacheService _rawCacheService;
    private readonly ILogger<ActivityExecutedEventConsumer> _logger;
    private readonly IBus _bus;
    private readonly IOrchestratorHealthMetricsService _metricsService;
    private readonly IOrchestratorFlowMetricsService _flowMetricsService;
    private static readonly ActivitySource ActivitySource = new("Manager.Orchestrator.Consumers");

    public ActivityExecutedEventConsumer(
        IOrchestrationCacheService orchestrationCacheService,
        ICacheService rawCacheService,
        ILogger<ActivityExecutedEventConsumer> logger,
        IBus bus,
        IOrchestratorHealthMetricsService metricsService,
        IOrchestratorFlowMetricsService flowMetricsService)
    {
        _orchestrationCacheService = orchestrationCacheService;
        _rawCacheService = rawCacheService;
        _logger = logger;
        _bus = bus;
        _metricsService = metricsService;
        _flowMetricsService = flowMetricsService;
    }

    public async Task Consume(ConsumeContext<ActivityExecutedEvent> context)
    {
        var activityEvent = context.Message;
        // ✅ Use the correlation ID from the event message instead of generating new one
        var correlationId = activityEvent.CorrelationId;

        using var activity = ActivitySource.StartActivityWithCorrelation("ProcessActivityExecutedEvent");
        activity?.SetTag("orchestratedFlowId", activityEvent.OrchestratedFlowEntityId.ToString())
                ?.SetTag("stepId", activityEvent.StepId.ToString())
                ?.SetTag("processorId", activityEvent.ProcessorId.ToString())
                ?.SetTag("executionId", activityEvent.ExecutionId.ToString())
                ?.SetTag("correlationId", correlationId.ToString());

        var stopwatch = Stopwatch.StartNew();
        var publishedCommands = 0;

        _logger.LogInformationWithCorrelation("Processing ActivityExecutedEvent. OrchestratedFlowId: {OrchestratedFlowId}, StepId: {StepId}, ProcessorId: {ProcessorId}, ExecutionId: {ExecutionId}, Status: {Status}, Duration: {Duration}ms, EntitiesProcessed: {EntitiesProcessed}",
            activityEvent.OrchestratedFlowEntityId, activityEvent.StepId, activityEvent.ProcessorId, activityEvent.ExecutionId, activityEvent.Status, activityEvent.Duration.TotalMilliseconds, activityEvent.EntitiesProcessed);

        try
        {
            // Record successful command consumption (ActivityExecutedEvent consumed)
            _flowMetricsService.RecordCommandConsumed(success: true, orchestratedFlowId: activityEvent.OrchestratedFlowEntityId);

            // Step 1: Read OrchestrationCacheModel from cache
            activity?.SetTag("step", "1-ReadOrchestrationCache");
            var orchestrationData = await _orchestrationCacheService.GetOrchestrationDataAsync(activityEvent.OrchestratedFlowEntityId);
            if (orchestrationData == null)
            {
                throw new InvalidOperationException($"Orchestration data not found in cache for OrchestratedFlowId: {activityEvent.OrchestratedFlowEntityId}");
            }

            // Step 2: Get the nextSteps collection from StepEntities
            activity?.SetTag("step", "2-GetNextSteps");
            if (!orchestrationData.StepManager.StepEntities.TryGetValue(activityEvent.StepId, out var currentStepEntity))
            {
                throw new InvalidOperationException($"Step entity not found for StepId: {activityEvent.StepId}");
            }

            var nextSteps = currentStepEntity.NextStepIds;
            activity?.SetTag("nextStepCount", nextSteps.Count);

            _logger.LogDebugWithCorrelation("Found {NextStepCount} next steps for StepId: {StepId}",
                nextSteps.Count, activityEvent.StepId);

            // Step 3: Check if nextSteps collection is empty (flow branch termination)
            if (nextSteps.Count == 0)
            {
                activity?.SetTag("step", "3-FlowTermination");
                await HandleFlowBranchTerminationAsync(activityEvent);
                
                stopwatch.Stop();
                activity?.SetTag("result", "FlowTerminated")
                        ?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds)
                        ?.SetStatus(ActivityStatusCode.Ok);

                _logger.LogInformationWithCorrelation("Workflow step completed. OrchestratedFlowId: {OrchestratedFlowId}, StepId: {StepId}, ExecutionId: {ExecutionId}, WorkflowPhase: {WorkflowPhase}, Duration: {Duration}ms",
                    activityEvent.OrchestratedFlowEntityId, activityEvent.StepId, activityEvent.ExecutionId, "FlowBranchTermination", stopwatch.ElapsedMilliseconds);
                return;
            }

            // Step 4: Process each next step
            activity?.SetTag("step", "4-ProcessNextSteps");
            await ProcessNextStepsAsync(activityEvent, nextSteps, orchestrationData);
            publishedCommands = nextSteps.Count;

            stopwatch.Stop();
            activity?.SetTag("publishedCommands", publishedCommands)
                    ?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds)
                    ?.SetTag("result", "Success")
                    ?.SetStatus(ActivityStatusCode.Ok);



            _logger.LogInformationWithCorrelation("Successfully processed ActivityExecutedEvent. OrchestratedFlowId: {OrchestratedFlowId}, StepId: {StepId}, NextSteps: {NextStepCount}, PublishedCommands: {PublishedCommands}, Duration: {Duration}ms",
                activityEvent.OrchestratedFlowEntityId, activityEvent.StepId, nextSteps.Count, publishedCommands, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message)
                    ?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds)
                    ?.SetTag("error.type", ex.GetType().Name)
                    ?.SetTag("result", "Error");

            // Record failed command consumption (ActivityExecutedEvent processing failed)
            _flowMetricsService.RecordCommandConsumed(success: false, orchestratedFlowId: activityEvent.OrchestratedFlowEntityId);

            // Record activity event processing exception as critical
            _metricsService.RecordException(ex.GetType().Name, "error", isCritical: true);

            // Record failed activity event processed metrics (use defaults if data not available)
            var stepVersionName = "unknown_unknown";
            var orchestratedFlowVersionName = "unknown_unknown";
            try
            {
                var orchestrationData = await _orchestrationCacheService.GetOrchestrationDataAsync(activityEvent.OrchestratedFlowEntityId);
                if (orchestrationData != null)
                {
                    orchestratedFlowVersionName = orchestrationData.OrchestratedFlow.GetCompositeKey();
                    if (orchestrationData.StepManager.StepEntities.TryGetValue(activityEvent.StepId, out var stepEntity))
                    {
                        stepVersionName = stepEntity.GetCompositeKey();
                    }
                }
            }
            catch
            {
                // Ignore errors when getting data for metrics - use defaults
            }



            _logger.LogErrorWithCorrelation(ex, "Error processing ActivityExecutedEvent. OrchestratedFlowId: {OrchestratedFlowId}, StepId: {StepId}, ProcessorId: {ProcessorId}, Duration: {Duration}ms, ErrorType: {ErrorType}",
                activityEvent.OrchestratedFlowEntityId, activityEvent.StepId, activityEvent.ProcessorId, stopwatch.ElapsedMilliseconds, ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Handles flow branch termination by deleting cache processor data
    /// </summary>
    /// <param name="activityEvent">The activity executed event</param>
    private async Task HandleFlowBranchTerminationAsync(ActivityExecutedEvent activityEvent)
    {
        using var activity = ActivitySource.StartActivity("HandleFlowBranchTermination");
        activity?.SetTag("orchestratedFlowId", activityEvent.OrchestratedFlowEntityId.ToString())
                ?.SetTag("stepId", activityEvent.StepId.ToString())
                ?.SetTag("processorId", activityEvent.ProcessorId.ToString());

        try
        {
            // Delete cache processor data using the standardized cache key method
            var mapName = activityEvent.ProcessorId.ToString();
            var key = _rawCacheService.GetCacheKey(activityEvent.OrchestratedFlowEntityId, activityEvent.CorrelationId, activityEvent.ExecutionId, activityEvent.StepId, activityEvent.PublishId);

            await _rawCacheService.RemoveAsync(mapName, key);

            _logger.LogInformationWithCorrelation("Deleted cache processor data for flow branch termination. MapName: {MapName}, Key: {Key}",
                mapName, key);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Record cache deletion exception as non-critical (cleanup operation)
            _metricsService.RecordException(ex.GetType().Name, "warning", isCritical: false);

            _logger.LogErrorWithCorrelation(ex, "Failed to delete cache processor data for flow branch termination. ProcessorId: {ProcessorId}, OrchestratedFlowId: {OrchestratedFlowId}, StepId: {StepId}",
                activityEvent.ProcessorId, activityEvent.OrchestratedFlowEntityId, activityEvent.StepId);
            throw;
        }
    }

    /// <summary>
    /// Processes all next steps by copying cache data and publishing ExecuteActivityCommand
    /// </summary>
    /// <param name="activityEvent">The activity executed event</param>
    /// <param name="nextSteps">Collection of next step IDs</param>
    /// <param name="orchestrationData">Orchestration cache data</param>
    private async Task ProcessNextStepsAsync(ActivityExecutedEvent activityEvent, List<Guid> nextSteps, OrchestrationCacheModel orchestrationData)
    {
        using var activity = ActivitySource.StartActivity("ProcessNextSteps");
        activity?.SetTag("nextStepCount", nextSteps.Count);

        var tasks = new List<Task>();

        foreach (var nextStepId in nextSteps)
        {
            var task = ProcessSingleNextStepAsync(activityEvent, nextStepId, orchestrationData);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Processes a single next step by copying cache data and publishing ExecuteActivityCommand
    /// </summary>
    /// <param name="activityEvent">The activity executed event</param>
    /// <param name="nextStepId">The next step ID to process</param>
    /// <param name="orchestrationData">Orchestration cache data</param>
    private async Task ProcessSingleNextStepAsync(ActivityExecutedEvent activityEvent, Guid nextStepId, OrchestrationCacheModel orchestrationData)
    {
        using var activity = ActivitySource.StartActivity("ProcessSingleNextStep");
        activity?.SetTag("nextStepId", nextStepId.ToString());

        try
        {
            // Get next step entity
            if (!orchestrationData.StepManager.StepEntities.TryGetValue(nextStepId, out var nextStepEntity))
            {
                throw new InvalidOperationException($"Next step entity not found for StepId: {nextStepId}");
            }

            // Handle entry conditions
            var shouldExecuteStep = ShouldExecuteStep(nextStepEntity.EntryCondition, activityEvent, isPreviousStepSuccess: true);
            activity?.SetTag("entryCondition", nextStepEntity.EntryCondition.ToString())
                    ?.SetTag("shouldExecuteStep", shouldExecuteStep);

            if (!shouldExecuteStep)
            {
                _logger.LogInformationWithCorrelation("Skipping step due to entry condition. NextStepId: {NextStepId}, EntryCondition: {EntryCondition}, PreviousStepSuccess: {PreviousStepSuccess}",
                    nextStepId, nextStepEntity.EntryCondition, true);
                return;
            }

            // Step 4.1: Generate new publishId for each command publication
            var publishId = Guid.NewGuid();

            // Step 4.2: Copy cache processor data from source to destination
            await CopyCacheProcessorDataAsync(activityEvent, nextStepId, nextStepEntity.ProcessorId, publishId);

            // Step 4.3: Get assignments for next step
            var assignmentModels = new List<AssignmentModel>();
            if (orchestrationData.AssignmentManager.Assignments.TryGetValue(nextStepId, out var assignments))
            {
                assignmentModels.AddRange(assignments);
            }

            // Step 4.4: Compose and publish ExecuteActivityCommand
            var command = new ExecuteActivityCommand
            {
                ProcessorId = nextStepEntity.ProcessorId,
                OrchestratedFlowEntityId = activityEvent.OrchestratedFlowEntityId,
                StepId = nextStepId,
                ExecutionId = activityEvent.ExecutionId,
                Entities = assignmentModels,
                CorrelationId = activityEvent.CorrelationId,
                PublishId = publishId // Use generated publishId instead of step relationship
            };

            var publishStopwatch = Stopwatch.StartNew();
            await _bus.Publish(command);
            publishStopwatch.Stop();

            // Record successful event publishing (ExecuteActivityCommand published)
            _flowMetricsService.RecordEventPublished(success: true, eventType: "ExecuteActivityCommand", orchestratedFlowId: activityEvent.OrchestratedFlowEntityId);

            _logger.LogInformationWithCorrelation("Workflow step transition. OrchestratedFlowId: {OrchestratedFlowId}, FromStepId: {FromStepId}, ToStepId: {ToStepId}, ExecutionId: {ExecutionId}, ProcessorId: {ProcessorId}, WorkflowPhase: {WorkflowPhase}, AssignmentCount: {AssignmentCount}, TransitionDuration: {TransitionDuration}ms",
                activityEvent.OrchestratedFlowEntityId, activityEvent.StepId, nextStepId, activityEvent.ExecutionId, nextStepEntity.ProcessorId, "StepTransition", assignmentModels.Count, publishStopwatch.ElapsedMilliseconds);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Record failed event publishing (ExecuteActivityCommand publishing failed)
            _flowMetricsService.RecordEventPublished(success: false, eventType: "ExecuteActivityCommand", orchestratedFlowId: activityEvent.OrchestratedFlowEntityId);

            // Record step command publishing exception as critical
            _metricsService.RecordException(ex.GetType().Name, "error", isCritical: true);

            _logger.LogErrorWithCorrelation(ex, "Failed to process next step. NextStepId: {NextStepId}", nextStepId);
            throw;
        }
        finally
        {
            // Delete source cache data in all cases (success, failure, or skip)
            await DeleteSourceCacheDataAsync(activityEvent);
        }
    }

    /// <summary>
    /// Copies cache processor data from source processor to destination processor
    /// </summary>
    /// <param name="activityEvent">The activity executed event</param>
    /// <param name="nextStepId">The next step ID</param>
    /// <param name="destinationProcessorId">The destination processor ID</param>
    /// <param name="publishId">The generated publish ID for the destination</param>

    private async Task CopyCacheProcessorDataAsync(ActivityExecutedEvent activityEvent, Guid nextStepId, Guid destinationProcessorId, Guid publishId)
    {
        using var activity = ActivitySource.StartActivity("CopyCacheProcessorData");
        activity?.SetTag("sourceProcessorId", activityEvent.ProcessorId.ToString())
                ?.SetTag("destinationProcessorId", destinationProcessorId.ToString())
                ?.SetTag("nextStepId", nextStepId.ToString());

        try
        {
            // Source cache location
            var sourceMapName = activityEvent.ProcessorId.ToString();
            var sourceKey = _rawCacheService.GetCacheKey(activityEvent.OrchestratedFlowEntityId, activityEvent.CorrelationId, activityEvent.ExecutionId, activityEvent.StepId, activityEvent.PublishId);

            // Destination cache location (use generated publishId for destination)
            var destinationMapName = destinationProcessorId.ToString();
            var destinationKey = _rawCacheService.GetCacheKey(activityEvent.OrchestratedFlowEntityId, activityEvent.CorrelationId, activityEvent.ExecutionId, nextStepId, publishId);

            // Copy data from source to destination
            var sourceData = await _rawCacheService.GetAsync(sourceMapName, sourceKey);
            if (!string.IsNullOrEmpty(sourceData))
            {
                await _rawCacheService.SetAsync(destinationMapName, destinationKey, sourceData);

                _logger.LogDebugWithCorrelation("Copied cache processor data. Source: {SourceMapName}:{SourceKey} -> Destination: {DestinationMapName}:{DestinationKey}",
                    sourceMapName, sourceKey, destinationMapName, destinationKey);
            }
            else
            {
                _logger.LogWarningWithCorrelation("No source data found to copy. Source: {SourceMapName}:{SourceKey}",
                    sourceMapName, sourceKey);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogErrorWithCorrelation(ex, "Failed to copy cache processor data. SourceProcessorId: {SourceProcessorId}, DestinationProcessorId: {DestinationProcessorId}",
                activityEvent.ProcessorId, destinationProcessorId);
            throw;
        }
    }

    /// <summary>
    /// Determines if a step should be executed based on its entry condition and previous step result
    /// </summary>
    /// <param name="entryCondition">The entry condition of the step</param>
    /// <param name="activityEvent">The activity event from the previous step</param>
    /// <param name="isPreviousStepSuccess">Whether the previous step was successful</param>
    /// <returns>True if the step should be executed, false otherwise</returns>
    private bool ShouldExecuteStep(StepEntryCondition entryCondition, ActivityExecutedEvent activityEvent, bool isPreviousStepSuccess)
    {
        return entryCondition switch
        {
            StepEntryCondition.PreviousSuccess => isPreviousStepSuccess,
            StepEntryCondition.PreviousFailure => !isPreviousStepSuccess,
            StepEntryCondition.Always => true,
            StepEntryCondition.Never => false,
            _ => true // Default to execute if unknown condition
        };
    }

    /// <summary>
    /// Deletes source cache data after processing
    /// </summary>
    /// <param name="activityEvent">The activity event</param>
    private async Task DeleteSourceCacheDataAsync(ActivityExecutedEvent activityEvent)
    {
        try
        {
            var sourceMapName = activityEvent.ProcessorId.ToString();
            var sourceKey = _rawCacheService.GetCacheKey(activityEvent.OrchestratedFlowEntityId, activityEvent.CorrelationId, activityEvent.ExecutionId, activityEvent.StepId, activityEvent.PublishId);

            await _rawCacheService.RemoveAsync(sourceMapName, sourceKey);

            _logger.LogDebugWithCorrelation("Deleted source cache data. Source: {SourceMapName}:{SourceKey}",
                sourceMapName, sourceKey);
        }
        catch (Exception ex)
        {
            // Record cache cleanup exception as non-critical (cleanup operation)
            _metricsService.RecordException(ex.GetType().Name, "warning", isCritical: false);

            _logger.LogErrorWithCorrelation(ex, "Failed to delete source cache data. ProcessorId: {ProcessorId}, StepId: {StepId}",
                activityEvent.ProcessorId, activityEvent.StepId);
            // Don't throw here as this is cleanup - log the error but continue
        }
    }

    /// <summary>
    /// Attempts to find the previous step ID for a given step by looking at which step has the current step in its NextStepIds
    /// </summary>
    /// <param name="currentStepId">The current step ID to find the previous step for</param>
    /// <param name="orchestrationData">Orchestration data containing step relationships</param>
    /// <returns>The previous step ID, or Guid.Empty if not found or if it's an entry point</returns>
    private Guid FindPreviousStepId(Guid currentStepId, OrchestrationCacheModel orchestrationData)
    {
        try
        {
            // Check if this is an entry point (no previous step)
            if (orchestrationData.EntryPoints.Contains(currentStepId))
            {
                return Guid.Empty;
            }

            // Look for a step that has currentStepId in its NextStepIds
            foreach (var stepEntity in orchestrationData.StepManager.StepEntities.Values)
            {
                if (stepEntity.NextStepIds.Contains(currentStepId))
                {
                    return stepEntity.Id;
                }
            }

            // If no previous step found, assume it's an entry point
            return Guid.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Failed to find previous step for StepId: {StepId}. Using Guid.Empty as fallback.", currentStepId);
            return Guid.Empty;
        }
    }
}
