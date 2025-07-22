using System.Diagnostics;
using Manager.Orchestrator.Models;
using Manager.Orchestrator.Services;
using MassTransit;
using Shared.Correlation;
using Shared.Entities.Enums;
using Shared.Extensions;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;
using Shared.Models;
using Shared.Services;

namespace Manager.Orchestrator.Consumers;

/// <summary>
/// Consumer for ActivityFailedEvent that handles workflow progression for failed activities
/// Manages the transition from failed steps to their next steps based on entry conditions
/// </summary>
public class ActivityFailedEventConsumer : IConsumer<ActivityFailedEvent>
{
    private readonly IOrchestrationCacheService _orchestrationCacheService;
    private readonly ICacheService _rawCacheService;
    private readonly ILogger<ActivityFailedEventConsumer> _logger;
    private readonly IBus _bus;
    private readonly IOrchestratorHealthMetricsService _metricsService;
    private readonly IOrchestratorFlowMetricsService _flowMetricsService;
    private static readonly ActivitySource ActivitySource = new("Manager.Orchestrator.Consumers");

    public ActivityFailedEventConsumer(
        IOrchestrationCacheService orchestrationCacheService,
        ICacheService rawCacheService,
        ILogger<ActivityFailedEventConsumer> logger,
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

    public async Task Consume(ConsumeContext<ActivityFailedEvent> context)
    {
        var activityEvent = context.Message;
        // ✅ Use the correlation ID from the event message instead of generating new one
        var correlationId = activityEvent.CorrelationId;

        using var activity = ActivitySource.StartActivityWithCorrelation("ProcessActivityFailedEvent");
        activity?.SetTag("orchestratedFlowId", activityEvent.OrchestratedFlowEntityId.ToString())
                ?.SetTag("stepId", activityEvent.StepId.ToString())
                ?.SetTag("processorId", activityEvent.ProcessorId.ToString())
                ?.SetTag("executionId", activityEvent.ExecutionId.ToString())
                ?.SetTag("correlationId", correlationId.ToString());

        var stopwatch = Stopwatch.StartNew();
        var publishedCommands = 0;

        _logger.LogInformationWithCorrelation("Processing ActivityFailedEvent. OrchestratedFlowId: {OrchestratedFlowId}, StepId: {StepId}, ProcessorId: {ProcessorId}, ExecutionId: {ExecutionId}, ErrorMessage: {ErrorMessage}",
            activityEvent.OrchestratedFlowEntityId, activityEvent.StepId, activityEvent.ProcessorId, activityEvent.ExecutionId, activityEvent.ErrorMessage);

        try
        {
            // Record successful command consumption (ActivityFailedEvent consumed)
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

            var nextSteps = currentStepEntity.NextStepIds.ToList();
            activity?.SetTag("nextStepCount", nextSteps.Count);

            // Step 3: Check if nextSteps collection is empty (flow branch termination)
            if (nextSteps.Count == 0)
            {
                activity?.SetTag("step", "3-FlowTermination");
                await HandleFlowBranchTerminationAsync(activityEvent);
                
                stopwatch.Stop();
                activity?.SetTag("result", "FlowTerminated")
                        ?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds)
                        ?.SetStatus(ActivityStatusCode.Ok);

                _logger.LogInformationWithCorrelation("Flow branch termination detected and processed for failed activity. OrchestratedFlowId: {OrchestratedFlowId}, StepId: {StepId}, Duration: {Duration}ms",
                    activityEvent.OrchestratedFlowEntityId, activityEvent.StepId, stopwatch.ElapsedMilliseconds);
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

            _logger.LogInformationWithCorrelation("Successfully processed ActivityFailedEvent. OrchestratedFlowId: {OrchestratedFlowId}, StepId: {StepId}, NextSteps: {NextStepCount}, PublishedCommands: {PublishedCommands}, Duration: {Duration}ms",
                activityEvent.OrchestratedFlowEntityId, activityEvent.StepId, nextSteps.Count, publishedCommands, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message)
                    ?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds)
                    ?.SetTag("error.type", ex.GetType().Name)
                    ?.SetTag("result", "Error");

            // Record failed command consumption (ActivityFailedEvent processing failed)
            _flowMetricsService.RecordCommandConsumed(success: false, orchestratedFlowId: activityEvent.OrchestratedFlowEntityId);

            // Record activity failed event processing exception as critical
            _metricsService.RecordException(ex.GetType().Name, "error", isCritical: true);

            _logger.LogErrorWithCorrelation(ex, "Error processing ActivityFailedEvent. OrchestratedFlowId: {OrchestratedFlowId}, StepId: {StepId}, ProcessorId: {ProcessorId}, Duration: {Duration}ms, ErrorType: {ErrorType}",
                activityEvent.OrchestratedFlowEntityId, activityEvent.StepId, activityEvent.ProcessorId, stopwatch.ElapsedMilliseconds, ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Handles flow branch termination by deleting cache processor data
    /// </summary>
    /// <param name="activityEvent">The activity failed event</param>
    private async Task HandleFlowBranchTerminationAsync(ActivityFailedEvent activityEvent)
    {
        using var activity = ActivitySource.StartActivity("HandleFlowBranchTermination");
        activity?.SetTag("processorId", activityEvent.ProcessorId.ToString())
                ?.SetTag("orchestratedFlowId", activityEvent.OrchestratedFlowEntityId.ToString())
                ?.SetTag("stepId", activityEvent.StepId.ToString());

        try
        {
            await DeleteSourceCacheDataAsync(activityEvent);
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
    /// <param name="activityEvent">The activity failed event</param>
    /// <param name="nextSteps">Collection of next step IDs</param>
    /// <param name="orchestrationData">Orchestration cache data</param>
    private async Task ProcessNextStepsAsync(ActivityFailedEvent activityEvent, List<Guid> nextSteps, OrchestrationCacheModel orchestrationData)
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
    /// <param name="activityEvent">The activity failed event</param>
    /// <param name="nextStepId">The next step ID to process</param>
    /// <param name="orchestrationData">Orchestration cache data</param>
    private async Task ProcessSingleNextStepAsync(ActivityFailedEvent activityEvent, Guid nextStepId, OrchestrationCacheModel orchestrationData)
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
            var shouldExecuteStep = ShouldExecuteStep(nextStepEntity.EntryCondition, activityEvent, isPreviousStepSuccess: false);
            activity?.SetTag("entryCondition", nextStepEntity.EntryCondition.ToString())
                    ?.SetTag("shouldExecuteStep", shouldExecuteStep);

            if (!shouldExecuteStep)
            {
                _logger.LogInformationWithCorrelation("Skipping step due to entry condition. NextStepId: {NextStepId}, EntryCondition: {EntryCondition}, PreviousStepSuccess: {PreviousStepSuccess}",
                    nextStepId, nextStepEntity.EntryCondition, false);
                return;
            }

            // Step 4.1: Copy cache processor data from source to destination
            await CopyCacheProcessorDataAsync(activityEvent, nextStepId, nextStepEntity.ProcessorId);

            // Step 4.2: Get assignments for next step
            var assignmentModels = new List<AssignmentModel>();
            if (orchestrationData.AssignmentManager.Assignments.TryGetValue(nextStepId, out var assignments))
            {
                assignmentModels.AddRange(assignments);
            }

            // Step 4.3: Compose and publish ExecuteActivityCommand
            var command = new ExecuteActivityCommand
            {
                ProcessorId = nextStepEntity.ProcessorId,
                OrchestratedFlowEntityId = activityEvent.OrchestratedFlowEntityId,
                StepId = nextStepId,
                ExecutionId = activityEvent.ExecutionId,
                Entities = assignmentModels,
                CorrelationId = activityEvent.CorrelationId,
                PublishId = Guid.NewGuid() // Generate new publishId for command publication
            };

            await _bus.Publish(command);

            // Record successful event publishing (ExecuteActivityCommand published after failure)
            _flowMetricsService.RecordEventPublished(success: true, eventType: "ExecuteActivityCommand", orchestratedFlowId: activityEvent.OrchestratedFlowEntityId);

            _logger.LogDebugWithCorrelation("Published ExecuteActivityCommand for next step after failure. NextStepId: {NextStepId}, ProcessorId: {ProcessorId}, AssignmentCount: {AssignmentCount}",
                nextStepId, nextStepEntity.ProcessorId, assignmentModels.Count);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Record failed event publishing (ExecuteActivityCommand publishing failed after failure)
            _flowMetricsService.RecordEventPublished(success: false, eventType: "ExecuteActivityCommand", orchestratedFlowId: activityEvent.OrchestratedFlowEntityId);

            // Record step processing after failure exception as critical
            _metricsService.RecordException(ex.GetType().Name, "error", isCritical: true);

            _logger.LogErrorWithCorrelation(ex, "Failed to process next step after failure. NextStepId: {NextStepId}", nextStepId);
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
    /// <param name="activityEvent">The activity failed event</param>
    /// <param name="nextStepId">The next step ID</param>
    /// <param name="destinationProcessorId">The destination processor ID</param>
    private async Task CopyCacheProcessorDataAsync(ActivityFailedEvent activityEvent, Guid nextStepId, Guid destinationProcessorId)
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

            // Destination cache location
            var destinationMapName = destinationProcessorId.ToString();
            var destinationKey = _rawCacheService.GetCacheKey(activityEvent.OrchestratedFlowEntityId, activityEvent.CorrelationId, activityEvent.ExecutionId, nextStepId, Guid.NewGuid());

            // Copy data from source to destination
            var sourceData = await _rawCacheService.GetAsync(sourceMapName, sourceKey);
            if (!string.IsNullOrEmpty(sourceData))
            {
                await _rawCacheService.SetAsync(destinationMapName, destinationKey, sourceData);

                _logger.LogDebugWithCorrelation("Copied cache processor data after failure. Source: {SourceMapName}:{SourceKey} -> Destination: {DestinationMapName}:{DestinationKey}",
                    sourceMapName, sourceKey, destinationMapName, destinationKey);
            }
            else
            {
                _logger.LogWarningWithCorrelation("No source data found to copy after failure. Source: {SourceMapName}:{SourceKey}",
                    sourceMapName, sourceKey);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogErrorWithCorrelation(ex, "Failed to copy cache processor data after failure. SourceProcessorId: {SourceProcessorId}, DestinationProcessorId: {DestinationProcessorId}",
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
    private bool ShouldExecuteStep(StepEntryCondition entryCondition, ActivityFailedEvent activityEvent, bool isPreviousStepSuccess)
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
    /// <param name="activityEvent">The activity failed event</param>
    private async Task DeleteSourceCacheDataAsync(ActivityFailedEvent activityEvent)
    {
        try
        {
            var sourceMapName = activityEvent.ProcessorId.ToString();
            var sourceKey = _rawCacheService.GetCacheKey(activityEvent.OrchestratedFlowEntityId, activityEvent.CorrelationId, activityEvent.ExecutionId, activityEvent.StepId, activityEvent.PublishId);

            await _rawCacheService.RemoveAsync(sourceMapName, sourceKey);

            _logger.LogDebugWithCorrelation("Deleted source cache data after failure. Source: {SourceMapName}:{SourceKey}",
                sourceMapName, sourceKey);
        }
        catch (Exception ex)
        {
            // Record cache cleanup exception as non-critical (cleanup operation)
            _metricsService.RecordException(ex.GetType().Name, "warning", isCritical: false);

            _logger.LogErrorWithCorrelation(ex, "Failed to delete source cache data after failure. ProcessorId: {ProcessorId}, StepId: {StepId}",
                activityEvent.ProcessorId, activityEvent.StepId);
            // Don't throw here as this is cleanup - log the error but continue
        }
    }
}
