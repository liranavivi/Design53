using System.Diagnostics;
using Manager.OrchestratedFlow.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.Entities;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.OrchestratedFlow.Consumers;

public class UpdateOrchestratedFlowCommandConsumer : IConsumer<UpdateOrchestratedFlowCommand>
{
    private readonly IOrchestratedFlowEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<UpdateOrchestratedFlowCommandConsumer> _logger;

    public UpdateOrchestratedFlowCommandConsumer(
        IOrchestratedFlowEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<UpdateOrchestratedFlowCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UpdateOrchestratedFlowCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing UpdateOrchestratedFlowCommand. Id: {Id}, Version: {Version}, Name: {Name}, WorkflowId: {WorkflowId}, AssignmentIds: {AssignmentIds}, RequestedBy: {RequestedBy}",
            command.Id, command.Version, command.Name, command.WorkflowId, string.Join(",", command.AssignmentIds), command.RequestedBy);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(command.Id);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("OrchestratedFlow entity not found for update. Id: {Id}", command.Id);
                await context.RespondAsync(new UpdateOrchestratedFlowCommandResponse
                {
                    Success = false,
                    Message = $"OrchestratedFlow entity with ID {command.Id} not found"
                });
                return;
            }

            var entity = new OrchestratedFlowEntity
            {
                Id = command.Id,
                Version = command.Version,
                Name = command.Name,
                Description = command.Description,
                WorkflowId = command.WorkflowId,
                AssignmentIds = command.AssignmentIds,
                UpdatedBy = command.RequestedBy,
                CreatedAt = existingEntity.CreatedAt,
                CreatedBy = existingEntity.CreatedBy
            };

            var updated = await _repository.UpdateAsync(entity);

            await _publishEndpoint.Publish(new OrchestratedFlowUpdatedEvent
            {
                Id = updated.Id,
                Version = updated.Version,
                Name = updated.Name,
                Description = updated.Description,
                WorkflowId = updated.WorkflowId,
                AssignmentIds = updated.AssignmentIds,
                UpdatedAt = updated.UpdatedAt,
                UpdatedBy = updated.UpdatedBy
            });

            stopwatch.Stop();
            _logger.LogInformationWithCorrelation("Successfully processed UpdateOrchestratedFlowCommand. Id: {Id}, Duration: {Duration}ms",
                updated.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new UpdateOrchestratedFlowCommandResponse
            {
                Success = true,
                Message = "OrchestratedFlow entity updated successfully"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing UpdateOrchestratedFlowCommand. Id: {Id}, Duration: {Duration}ms",
                command.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new UpdateOrchestratedFlowCommandResponse
            {
                Success = false,
                Message = $"Failed to update OrchestratedFlow entity: {ex.Message}"
            });
        }
    }
}

public class UpdateOrchestratedFlowCommandResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
