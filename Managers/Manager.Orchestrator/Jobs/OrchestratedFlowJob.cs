using MassTransit;
using Quartz;
using Shared.Correlation;
using Shared.MassTransit.Commands;
using Shared.Models;
using Manager.Orchestrator.Services;
using Shared.Services;
using System.Diagnostics;

namespace Manager.Orchestrator.Jobs;

/// <summary>
/// Quartz job that executes orchestrated flow entry points on a scheduled basis.
/// This job is responsible for triggering the execution of orchestrated flows based on cron expressions.
/// </summary>
[DisallowConcurrentExecution] // Prevent multiple instances of the same job running simultaneously
public class OrchestratedFlowJob : IJob
{
    private readonly IOrchestrationCacheService _orchestrationCacheService;
    private readonly IOrchestrationService _orchestrationService;
    private readonly IOrchestrationSchedulerService _schedulerService;
    private readonly IBus _bus;
    private readonly ILogger<OrchestratedFlowJob> _logger;
    private readonly ICacheService _rawCacheService;
    private readonly IOrchestratorHealthMetricsService _metricsService;
    private readonly IOrchestratorFlowMetricsService _flowMetricsService;

    /// <summary>
    /// Initializes a new instance of the OrchestratedFlowJob class.
    /// </summary>
    /// <param name="orchestrationCacheService">Service for orchestration cache operations</param>
    /// <param name="orchestrationService">Service for orchestration business logic including health validation</param>
    /// <param name="schedulerService">Service for scheduler operations</param>
    /// <param name="rawCacheService"></param>
    /// <param name="bus">MassTransit bus for publishing commands</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="metricsService">Metrics service for recording orchestrator metrics</param>
    /// <param name="flowMetricsService">Flow metrics service for recording flow metrics</param>
    public OrchestratedFlowJob(
        IOrchestrationCacheService orchestrationCacheService,
        IOrchestrationService orchestrationService,
        IOrchestrationSchedulerService schedulerService,
        ICacheService rawCacheService,
        IBus bus,
        ILogger<OrchestratedFlowJob> logger,
        IOrchestratorHealthMetricsService metricsService,
        IOrchestratorFlowMetricsService flowMetricsService)
    {
        _orchestrationCacheService = orchestrationCacheService;
        _orchestrationService = orchestrationService;
        _schedulerService = schedulerService;
        _rawCacheService = rawCacheService;
        _bus = bus;
        _logger = logger;
        _metricsService = metricsService;
        _flowMetricsService = flowMetricsService;
    }

    /// <summary>
    /// Executes the orchestrated flow job.
    /// This method is called by Quartz scheduler based on the configured cron expression.
    /// </summary>
    /// <param name="context">Job execution context containing job data</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task Execute(IJobExecutionContext context)
    {
        var jobDataMap = context.JobDetail.JobDataMap;
        var orchestratedFlowId = Guid.Parse(jobDataMap.GetString("OrchestratedFlowId")!);

        // Use stored correlation ID to maintain correlation chain, or generate new one for truly scheduled jobs
        var originalCorrelationIdString = jobDataMap.GetString("OriginalCorrelationId");
        var correlationId = originalCorrelationIdString != null
            ? Guid.Parse(originalCorrelationIdString)
            : Guid.NewGuid();

        var correlationSource = originalCorrelationIdString != null ? "inherited" : "generated";
        CorrelationIdContext.SetCorrelationIdStatic(correlationId);

        _logger.LogInformationWithCorrelation("Starting scheduled execution of orchestrated flow {OrchestratedFlowId}. CorrelationId: {CorrelationId} ({CorrelationSource})",
            orchestratedFlowId, correlationId, correlationSource);

        try
        {
            // Get orchestration cache data
            var orchestrationData = await _orchestrationCacheService.GetOrchestrationDataAsync(orchestratedFlowId);
            if (orchestrationData == null)
            {
                _logger.LogWarningWithCorrelation("Orchestration data not found for {OrchestratedFlowId}. Skipping scheduled execution.", orchestratedFlowId);
                return;
            }

            // Get cached entry points (already calculated and validated during orchestration setup)
            var entryPoints = orchestrationData.EntryPoints;

            if (!entryPoints.Any())
            {
                _logger.LogWarningWithCorrelation("No entry points found in cached orchestration data for {OrchestratedFlowId}. Skipping scheduled execution.", orchestratedFlowId);
                return;
            }

            _logger.LogInformationWithCorrelation("Using {EntryPointCount} cached entry points for scheduled execution of {OrchestratedFlowId}", entryPoints.Count, orchestratedFlowId);

            // Step 6: Check processor health
            _logger.LogDebugWithCorrelation("Validating processor health before scheduled execution of {OrchestratedFlowId}", orchestratedFlowId);
            var processorIds = orchestrationData.StepManager.ProcessorIds;
            var isHealthy = await _orchestrationService.ValidateProcessorHealthForExecutionAsync(processorIds);

            if (!isHealthy)
            {
                _logger.LogWarningWithCorrelation("Processor health validation failed for scheduled execution of {OrchestratedFlowId}. " +
                    "Skipping execution due to unhealthy processors. ProcessorCount: {ProcessorCount}",
                    orchestratedFlowId, processorIds.Count);
                return;
            }

            _logger.LogInformationWithCorrelation("All {ProcessorCount} processors are healthy. Proceeding with scheduled execution of {OrchestratedFlowId}",
                processorIds.Count, orchestratedFlowId);

            // Execute entry points (using the same logic as manual execution)
            await ExecuteEntryPointsAsync(orchestratedFlowId, entryPoints, orchestrationData, correlationId);

            _logger.LogInformationWithCorrelation("Successfully completed scheduled execution of orchestrated flow {OrchestratedFlowId}", orchestratedFlowId);

            // Check if this is a one-time execution and stop the scheduler
            await HandleOneTimeExecutionAsync(orchestratedFlowId, orchestrationData.OrchestratedFlow);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Failed to execute scheduled orchestrated flow {OrchestratedFlowId}", orchestratedFlowId);

            // Entry point execution metrics removed for volume optimization

            // Re-throw to let Quartz handle the failure (retry policies, etc.)
            throw;
        }
    }

    /// <summary>
    /// Executes the entry points for the orchestrated flow.
    /// This method contains the core logic for publishing ExecuteActivityCommand for each entry point.
    /// </summary>
    /// <param name="orchestratedFlowId">ID of the orchestrated flow</param>
    /// <param name="entryPoints">List of entry point step IDs</param>
    /// <param name="orchestrationData">Cached orchestration data</param>
    /// <param name="correlationId">Correlation ID for this execution</param>
    /// <returns>Task representing the asynchronous operation</returns>
    private async Task ExecuteEntryPointsAsync(
        Guid orchestratedFlowId,
        List<Guid> entryPoints,
        Models.OrchestrationCacheModel orchestrationData,
        Guid correlationId)
    {
        var stopwatch = Stopwatch.StartNew();
        var executionTasks = new List<Task>();
        var publishedCount = 0;

        foreach (var entryPoint in entryPoints)
        {
            try
            {
                // Get assignments for this entry point step
                var assignmentModels = new List<AssignmentModel>();
                if (orchestrationData.AssignmentManager.Assignments.TryGetValue(entryPoint, out List<AssignmentModel>? assignments) && assignments != null)
                {
                    assignmentModels.AddRange(assignments);
                }

                // Get processor ID for this step
                var processorId = orchestrationData.StepManager.StepEntities[entryPoint].ProcessorId;

                // Generate new execution ID for each entry point, but use orchestration correlation ID
                var executionId = Guid.NewGuid();

                // Write ProcessedActivityData.Data example to processor cache before publishing
                var mapName = processorId.ToString();
                var cacheKey = _rawCacheService.GetCacheKey(orchestratedFlowId, correlationId, executionId, entryPoint, Guid.Empty);

                // Create ProcessedActivityData.Data example for testing schemas between orchestrator and processor
                var processedActivityData = new
                {
                    processorId = processorId.ToString(),
                    orchestratedFlowEntityId = orchestratedFlowId.ToString(),
                    entitiesProcessed = assignmentModels.Count,
                    processingDetails = new
                    {
                        processedAt = DateTime.UtcNow,
                        processingDuration = "0ms",
                        inputDataReceived = true,
                        inputMetadataReceived = true,
                        sampleData = $"Entry point initialization data for step {entryPoint}",
                        entityTypes = assignmentModels.Select(e => e.GetType().Name).Distinct().ToArray(),
                        entities = assignmentModels.Select(e => new
                        {
                            entityId = e.EntityId.ToString(),
                            type = e.GetType().Name,
                            assignmentType = e.GetType().Name
                        }).ToArray()
                    }
                };

                var exampleData = System.Text.Json.JsonSerializer.Serialize(processedActivityData, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });

                // Store example data in processor cache
                await _rawCacheService.SetAsync(mapName, cacheKey, exampleData);

                _logger.LogDebugWithCorrelation("Stored example data in processor cache. MapName: {MapName}, Key: {Key}, Data: {Data}",
                    mapName, cacheKey, exampleData);

                // Create ExecuteActivityCommand
                var command = new ExecuteActivityCommand
                {
                    ProcessorId = processorId,
                    OrchestratedFlowEntityId = orchestratedFlowId,
                    StepId = entryPoint,
                    ExecutionId = executionId,
                    Entities = assignmentModels,
                    CorrelationId = correlationId,
                    PreviousStepId = Guid.Empty // Entry points have no previous step
                };

                _logger.LogInformationWithCorrelation("Workflow step initiated. OrchestratedFlowId: {OrchestratedFlowId}, StepId: {StepId}, ExecutionId: {ExecutionId}, ProcessorId: {ProcessorId}, WorkflowPhase: {WorkflowPhase}, AssignmentCount: {AssignmentCount}",
                    orchestratedFlowId, entryPoint, executionId, processorId, "EntryPointStart", assignmentModels.Count);

                // Publish command to processor (async)
                var publishTask = _bus.Publish(command);
                executionTasks.Add(publishTask);
                publishedCount++;

                // Record successful event publishing (ExecuteActivityCommand published for entry point)
                _flowMetricsService.RecordEventPublished(success: true, eventType: "ExecuteActivityCommand", orchestratedFlowId: orchestratedFlowId);

                _logger.LogInformationWithCorrelation("Workflow step command published. OrchestratedFlowId: {OrchestratedFlowId}, StepId: {StepId}, ExecutionId: {ExecutionId}, ProcessorId: {ProcessorId}, WorkflowPhase: {WorkflowPhase}",
                    orchestratedFlowId, entryPoint, executionId, processorId, "CommandPublished");
            }
            catch (Exception ex)
            {
                // Record failed event publishing (ExecuteActivityCommand publishing failed for entry point)
                _flowMetricsService.RecordEventPublished(success: false, eventType: "ExecuteActivityCommand", orchestratedFlowId: orchestratedFlowId);

                _logger.LogErrorWithCorrelation(ex, "Failed to execute entry point {StepId} for orchestration {OrchestratedFlowId}",
                    entryPoint, orchestratedFlowId);

                stopwatch.Stop();
                throw;
            }
        }

        // Wait for all publish operations to complete
        if (executionTasks.Any())
        {
            await Task.WhenAll(executionTasks);
            _logger.LogInformationWithCorrelation("Successfully published {PublishedCount} ExecuteActivityCommands for scheduled execution of {OrchestratedFlowId}",
                publishedCount, orchestratedFlowId);
        }

        stopwatch.Stop();

        // Entry point execution metrics removed for volume optimization
    }

    /// <summary>
    /// Handles one-time execution logic by stopping the scheduler if the flow is configured for one-time execution
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID</param>
    /// <param name="orchestratedFlow">The orchestrated flow entity</param>
    private async Task HandleOneTimeExecutionAsync(Guid orchestratedFlowId, Shared.Entities.OrchestratedFlowEntity orchestratedFlow)
    {
        try
        {
            if (orchestratedFlow.IsOneTimeExecution)
            {
                _logger.LogInformationWithCorrelation(
                    "One-time execution completed. Stopping scheduler for orchestrated flow {OrchestratedFlowId}",
                    orchestratedFlowId);

                await _schedulerService.StopSchedulerAsync(orchestratedFlowId);

                _logger.LogInformationWithCorrelation(
                    "Successfully stopped one-time execution scheduler for orchestrated flow {OrchestratedFlowId}",
                    orchestratedFlowId);
            }
        }
        catch (Exception ex)
        {
            // Log the scheduler stop failure but don't re-throw
            // The job execution was successful, scheduler cleanup failure shouldn't fail the job
            _logger.LogWarningWithCorrelation(ex,
                "Job executed successfully but failed to stop one-time scheduler for orchestrated flow {OrchestratedFlowId}",
                orchestratedFlowId);
        }
    }
}
