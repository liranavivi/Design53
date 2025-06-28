namespace Manager.Orchestrator.Services;

/// <summary>
/// Service interface for managing Quartz schedulers for orchestrated flows.
/// Provides functionality to start, stop, and manage scheduled execution of orchestrated flows.
/// </summary>
public interface IOrchestrationSchedulerService
{
    /// <summary>
    /// Starts the scheduler for the specified orchestrated flow.
    /// Creates a new Quartz job with the provided cron expression.
    /// </summary>
    /// <param name="orchestratedFlowId">ID of the orchestrated flow to schedule</param>
    /// <param name="cronExpression">Cron expression defining the schedule</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    /// <exception cref="ArgumentException">Thrown when cron expression is invalid</exception>
    /// <exception cref="InvalidOperationException">Thrown when scheduler is already running for this flow</exception>
    Task StartSchedulerAsync(Guid orchestratedFlowId, string cronExpression, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the scheduler for the specified orchestrated flow.
    /// Removes the Quartz job and cancels any pending executions.
    /// </summary>
    /// <param name="orchestratedFlowId">ID of the orchestrated flow to stop scheduling</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    /// <exception cref="InvalidOperationException">Thrown when no scheduler is running for this flow</exception>
    Task StopSchedulerAsync(Guid orchestratedFlowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the scheduler for the specified orchestrated flow with a new cron expression.
    /// If scheduler is running, it will be stopped and restarted with the new schedule.
    /// </summary>
    /// <param name="orchestratedFlowId">ID of the orchestrated flow to update</param>
    /// <param name="cronExpression">New cron expression defining the schedule</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    /// <exception cref="ArgumentException">Thrown when cron expression is invalid</exception>
    Task UpdateSchedulerAsync(Guid orchestratedFlowId, string cronExpression, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a scheduler is currently running for the specified orchestrated flow.
    /// </summary>
    /// <param name="orchestratedFlowId">ID of the orchestrated flow to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if scheduler is running, false otherwise</returns>
    Task<bool> IsSchedulerRunningAsync(Guid orchestratedFlowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current cron expression for the specified orchestrated flow scheduler.
    /// </summary>
    /// <param name="orchestratedFlowId">ID of the orchestrated flow</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current cron expression, or null if no scheduler is running</returns>
    Task<string?> GetSchedulerCronExpressionAsync(Guid orchestratedFlowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next scheduled execution time for the specified orchestrated flow.
    /// </summary>
    /// <param name="orchestratedFlowId">ID of the orchestrated flow</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Next execution time, or null if no scheduler is running</returns>
    Task<DateTimeOffset?> GetNextExecutionTimeAsync(Guid orchestratedFlowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if the provided cron expression is valid.
    /// </summary>
    /// <param name="cronExpression">Cron expression to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    bool ValidateCronExpression(string cronExpression);

    /// <summary>
    /// Gets a list of all currently scheduled orchestrated flows.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of orchestrated flow IDs that have active schedulers</returns>
    Task<List<Guid>> GetScheduledFlowsAsync(CancellationToken cancellationToken = default);
}
