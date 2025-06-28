using Quartz;
using Shared.Correlation;
using Manager.Orchestrator.Jobs;

namespace Manager.Orchestrator.Services;

/// <summary>
/// Service for managing Quartz schedulers for orchestrated flows.
/// Handles starting, stopping, and managing scheduled execution of orchestrated flows.
/// </summary>
public class OrchestrationSchedulerService : IOrchestrationSchedulerService
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<OrchestrationSchedulerService> _logger;
    private IScheduler? _scheduler;

    /// <summary>
    /// Initializes a new instance of the OrchestrationSchedulerService class.
    /// </summary>
    /// <param name="schedulerFactory">Quartz scheduler factory</param>
    /// <param name="logger">Logger instance</param>
    public OrchestrationSchedulerService(
        ISchedulerFactory schedulerFactory,
        ILogger<OrchestrationSchedulerService> logger)
    {
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Ensures the scheduler is initialized and started.
    /// </summary>
    /// <returns>Task representing the asynchronous operation</returns>
    private async Task EnsureSchedulerStartedAsync()
    {
        if (_scheduler == null)
        {
            _scheduler = await _schedulerFactory.GetScheduler();
            if (!_scheduler.IsStarted)
            {
                await _scheduler.Start();
                _logger.LogInformationWithCorrelation("Quartz scheduler started for orchestration service");
            }
        }
    }

    /// <summary>
    /// Creates a job key for the specified orchestrated flow.
    /// </summary>
    /// <param name="orchestratedFlowId">ID of the orchestrated flow</param>
    /// <returns>Job key</returns>
    private static JobKey CreateJobKey(Guid orchestratedFlowId)
    {
        return new JobKey($"orchestrated-flow-{orchestratedFlowId}", "orchestrated-flows");
    }

    /// <summary>
    /// Creates a trigger key for the specified orchestrated flow.
    /// </summary>
    /// <param name="orchestratedFlowId">ID of the orchestrated flow</param>
    /// <returns>Trigger key</returns>
    private static TriggerKey CreateTriggerKey(Guid orchestratedFlowId)
    {
        return new TriggerKey($"orchestrated-flow-trigger-{orchestratedFlowId}", "orchestrated-flows");
    }

    /// <inheritdoc />
    public async Task StartSchedulerAsync(Guid orchestratedFlowId, string cronExpression, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            throw new ArgumentException("Cron expression cannot be null or empty", nameof(cronExpression));
        }

        if (!ValidateCronExpression(cronExpression))
        {
            throw new ArgumentException($"Invalid cron expression: {cronExpression}", nameof(cronExpression));
        }

        await EnsureSchedulerStartedAsync();

        var jobKey = CreateJobKey(orchestratedFlowId);
        var triggerKey = CreateTriggerKey(orchestratedFlowId);

        // Check if job already exists
        if (await _scheduler!.CheckExists(jobKey, cancellationToken))
        {
            throw new InvalidOperationException($"Scheduler already running for orchestrated flow {orchestratedFlowId}");
        }

        try
        {
            // Get current correlation ID to maintain correlation chain across scheduled executions
            var existingCorrelationId = CorrelationIdContext.GetCurrentCorrelationIdStatic();
            var currentCorrelationId = existingCorrelationId != Guid.Empty ? existingCorrelationId : Guid.NewGuid();
            var correlationSource = existingCorrelationId != Guid.Empty ? "existing context" : "newly generated";

            _logger.LogDebugWithCorrelation("Creating scheduled job with correlation ID {CorrelationId} from {CorrelationSource} for orchestrated flow {OrchestratedFlowId}",
                currentCorrelationId, correlationSource, orchestratedFlowId);

            // Create job
            var job = JobBuilder.Create<OrchestratedFlowJob>()
                .WithIdentity(jobKey)
                .WithDescription($"Scheduled execution for orchestrated flow {orchestratedFlowId}")
                .UsingJobData("OrchestratedFlowId", orchestratedFlowId.ToString())
                .UsingJobData("OriginalCorrelationId", currentCorrelationId.ToString())
                .Build();

            // Create trigger with cron expression
            var trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .WithDescription($"Cron trigger for orchestrated flow {orchestratedFlowId}")
                .WithCronSchedule(cronExpression)
                .Build();

            // Schedule the job
            await _scheduler.ScheduleJob(job, trigger, cancellationToken);

            _logger.LogInformationWithCorrelation("Started scheduler for orchestrated flow {OrchestratedFlowId} with cron expression '{CronExpression}'", 
                orchestratedFlowId, cronExpression);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Failed to start scheduler for orchestrated flow {OrchestratedFlowId}", orchestratedFlowId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task StopSchedulerAsync(Guid orchestratedFlowId, CancellationToken cancellationToken = default)
    {
        await EnsureSchedulerStartedAsync();

        var jobKey = CreateJobKey(orchestratedFlowId);

        // Check if job exists
        if (!await _scheduler!.CheckExists(jobKey, cancellationToken))
        {
            throw new InvalidOperationException($"No scheduler running for orchestrated flow {orchestratedFlowId}");
        }

        try
        {
            // Delete the job (this also removes associated triggers)
            var deleted = await _scheduler.DeleteJob(jobKey, cancellationToken);
            
            if (deleted)
            {
                _logger.LogInformationWithCorrelation("Stopped scheduler for orchestrated flow {OrchestratedFlowId}", orchestratedFlowId);
            }
            else
            {
                _logger.LogWarningWithCorrelation("Failed to stop scheduler for orchestrated flow {OrchestratedFlowId} - job may have already been removed", orchestratedFlowId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Failed to stop scheduler for orchestrated flow {OrchestratedFlowId}", orchestratedFlowId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateSchedulerAsync(Guid orchestratedFlowId, string cronExpression, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            throw new ArgumentException("Cron expression cannot be null or empty", nameof(cronExpression));
        }

        if (!ValidateCronExpression(cronExpression))
        {
            throw new ArgumentException($"Invalid cron expression: {cronExpression}", nameof(cronExpression));
        }

        await EnsureSchedulerStartedAsync();

        var jobKey = CreateJobKey(orchestratedFlowId);
        var triggerKey = CreateTriggerKey(orchestratedFlowId);

        try
        {
            if (await _scheduler!.CheckExists(jobKey, cancellationToken))
            {
                // Job exists, update the trigger
                var newTrigger = TriggerBuilder.Create()
                    .WithIdentity(triggerKey)
                    .WithDescription($"Updated cron trigger for orchestrated flow {orchestratedFlowId}")
                    .WithCronSchedule(cronExpression)
                    .Build();

                await _scheduler.RescheduleJob(triggerKey, newTrigger, cancellationToken);
                
                _logger.LogInformationWithCorrelation("Updated scheduler for orchestrated flow {OrchestratedFlowId} with new cron expression '{CronExpression}'", 
                    orchestratedFlowId, cronExpression);
            }
            else
            {
                // Job doesn't exist, create new one
                await StartSchedulerAsync(orchestratedFlowId, cronExpression, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Failed to update scheduler for orchestrated flow {OrchestratedFlowId}", orchestratedFlowId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsSchedulerRunningAsync(Guid orchestratedFlowId, CancellationToken cancellationToken = default)
    {
        await EnsureSchedulerStartedAsync();
        var jobKey = CreateJobKey(orchestratedFlowId);
        return await _scheduler!.CheckExists(jobKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string?> GetSchedulerCronExpressionAsync(Guid orchestratedFlowId, CancellationToken cancellationToken = default)
    {
        await EnsureSchedulerStartedAsync();
        
        var triggerKey = CreateTriggerKey(orchestratedFlowId);
        var trigger = await _scheduler!.GetTrigger(triggerKey, cancellationToken);
        
        if (trigger is ICronTrigger cronTrigger)
        {
            return cronTrigger.CronExpressionString;
        }
        
        return null;
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetNextExecutionTimeAsync(Guid orchestratedFlowId, CancellationToken cancellationToken = default)
    {
        await EnsureSchedulerStartedAsync();

        var triggerKey = CreateTriggerKey(orchestratedFlowId);
        var triggerState = await _scheduler!.GetTriggerState(triggerKey, cancellationToken);

        if (triggerState == TriggerState.None)
        {
            return null;
        }

        var triggers = await _scheduler.GetTriggersOfJob(CreateJobKey(orchestratedFlowId), cancellationToken);
        var trigger = triggers.FirstOrDefault();

        if (trigger is ICronTrigger cronTrigger && !string.IsNullOrEmpty(cronTrigger.CronExpressionString))
        {
            // For cron triggers, we need to calculate the next fire time manually
            var cronExpression = new CronExpression(cronTrigger.CronExpressionString);
            return cronExpression.GetNextValidTimeAfter(DateTimeOffset.UtcNow);
        }

        return trigger?.GetFireTimeAfter(DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public bool ValidateCronExpression(string cronExpression)
    {
        try
        {
            CronExpression.ValidateExpression(cronExpression);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<List<Guid>> GetScheduledFlowsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchedulerStartedAsync();

        var jobKeys = await _scheduler!.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.GroupEquals("orchestrated-flows"), cancellationToken);

        var flowIds = new List<Guid>();
        foreach (var jobKey in jobKeys)
        {
            // Extract GUID from job key name (format: "orchestrated-flow-{guid}")
            var keyName = jobKey.Name;
            if (keyName.StartsWith("orchestrated-flow-") && keyName.Length > 18)
            {
                var guidString = keyName.Substring(18);
                if (Guid.TryParse(guidString, out var flowId))
                {
                    flowIds.Add(flowId);
                }
            }
        }

        return flowIds;
    }
}
