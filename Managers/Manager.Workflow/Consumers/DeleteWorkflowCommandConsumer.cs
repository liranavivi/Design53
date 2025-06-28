using System.Diagnostics;
using Manager.Workflow.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.Workflow.Consumers;
public class DeleteWorkflowCommandConsumer : IConsumer<DeleteWorkflowCommand>
{
    private readonly IWorkflowEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<DeleteWorkflowCommandConsumer> _logger;

    public DeleteWorkflowCommandConsumer(
        IWorkflowEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<DeleteWorkflowCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DeleteWorkflowCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing DeleteWorkflowCommand. Id: {Id}, RequestedBy: {RequestedBy}",
            command.Id, command.RequestedBy);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(command.Id);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Workflow entity not found for deletion. Id: {Id}", command.Id);
                await context.RespondAsync(new DeleteWorkflowCommandResponse
                {
                    Success = false,
                    Message = $"Workflow entity with ID {command.Id} not found"
                });
                return;
            }

            var success = await _repository.DeleteAsync(command.Id);

            if (success)
            {
                await _publishEndpoint.Publish(new WorkflowDeletedEvent
                {
                    Id = command.Id,
                    DeletedAt = DateTime.UtcNow,
                    DeletedBy = command.RequestedBy
                });

                stopwatch.Stop();
                _logger.LogInformationWithCorrelation("Successfully processed DeleteWorkflowCommand. Id: {Id}, Duration: {Duration}ms",
                    command.Id, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new DeleteWorkflowCommandResponse
                {
                    Success = true,
                    Message = "Workflow entity deleted successfully"
                });
            }
            else
            {
                stopwatch.Stop();
                _logger.LogWarningWithCorrelation("Failed to delete Workflow entity. Id: {Id}, Duration: {Duration}ms",
                    command.Id, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new DeleteWorkflowCommandResponse
                {
                    Success = false,
                    Message = "Failed to delete Workflow entity"
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing DeleteWorkflowCommand. Id: {Id}, Duration: {Duration}ms",
                command.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new DeleteWorkflowCommandResponse
            {
                Success = false,
                Message = $"Failed to delete Workflow entity: {ex.Message}"
            });
        }
    }
}

public class DeleteWorkflowCommandResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
