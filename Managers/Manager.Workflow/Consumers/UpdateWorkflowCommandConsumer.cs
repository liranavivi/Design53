using System.Diagnostics;
using Manager.Workflow.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.Entities;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.Workflow.Consumers;

public class UpdateWorkflowCommandConsumer : IConsumer<UpdateWorkflowCommand>
{
    private readonly IWorkflowEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<UpdateWorkflowCommandConsumer> _logger;

    public UpdateWorkflowCommandConsumer(
        IWorkflowEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<UpdateWorkflowCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UpdateWorkflowCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing UpdateWorkflowCommand. Id: {Id}, Version: {Version}, Name: {Name}, StepIds: {StepIds}, RequestedBy: {RequestedBy}",
            command.Id, command.Version, command.Name, string.Join(",", command.StepIds), command.RequestedBy);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(command.Id);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Workflow entity not found for update. Id: {Id}", command.Id);
                await context.RespondAsync(new UpdateWorkflowCommandResponse
                {
                    Success = false,
                    Message = $"Workflow entity with ID {command.Id} not found"
                });
                return;
            }

            var entity = new WorkflowEntity
            {
                Id = command.Id,
                Version = command.Version,
                Name = command.Name,
                Description = command.Description,
                StepIds = command.StepIds,
                UpdatedBy = command.RequestedBy,
                CreatedAt = existingEntity.CreatedAt,
                CreatedBy = existingEntity.CreatedBy
            };

            var updated = await _repository.UpdateAsync(entity);

            await _publishEndpoint.Publish(new WorkflowUpdatedEvent
            {
                Id = updated.Id,
                Version = updated.Version,
                Name = updated.Name,
                Description = updated.Description,
                StepIds = updated.StepIds,
                UpdatedAt = updated.UpdatedAt,
                UpdatedBy = updated.UpdatedBy
            });

            stopwatch.Stop();
            _logger.LogInformationWithCorrelation("Successfully processed UpdateWorkflowCommand. Id: {Id}, Duration: {Duration}ms",
                updated.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new UpdateWorkflowCommandResponse
            {
                Success = true,
                Message = "Workflow entity updated successfully"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing UpdateWorkflowCommand. Id: {Id}, Duration: {Duration}ms",
                command.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new UpdateWorkflowCommandResponse
            {
                Success = false,
                Message = $"Failed to update Workflow entity: {ex.Message}"
            });
        }
    }
}

public class UpdateWorkflowCommandResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
