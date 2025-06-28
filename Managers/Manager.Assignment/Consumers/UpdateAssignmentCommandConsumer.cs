using System.Diagnostics;
using Manager.Assignment.Repositories;
using MassTransit;
using Shared.Entities;
using Shared.Correlation;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.Assignment.Consumers;

public class UpdateAssignmentCommandConsumer : IConsumer<UpdateAssignmentCommand>
{
    private readonly IAssignmentEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<UpdateAssignmentCommandConsumer> _logger;

    public UpdateAssignmentCommandConsumer(
        IAssignmentEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<UpdateAssignmentCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UpdateAssignmentCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing UpdateAssignmentCommand. Id: {Id}, Version: {Version}, Name: {Name}, StepId: {StepId}, EntityIds count: {EntityIdsCount}, RequestedBy: {RequestedBy}",
            command.Id, command.Version, command.Name, command.StepId, command.EntityIds?.Count ?? 0, command.RequestedBy);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(command.Id);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Assignment entity not found for update. Id: {Id}", command.Id);
                await context.RespondAsync(new UpdateAssignmentCommandResponse
                {
                    Success = false,
                    Message = $"Assignment entity with ID {command.Id} not found"
                });
                return;
            }

            var entity = new AssignmentEntity
            {
                Id = command.Id,
                Version = command.Version,
                Name = command.Name,
                Description = command.Description,
                StepId = command.StepId,
                EntityIds = command.EntityIds ?? new List<Guid>(),
                UpdatedBy = command.RequestedBy,
                CreatedAt = existingEntity.CreatedAt,
                CreatedBy = existingEntity.CreatedBy
            };

            var updated = await _repository.UpdateAsync(entity);

            await _publishEndpoint.Publish(new AssignmentUpdatedEvent
            {
                Id = updated.Id,
                Version = updated.Version,
                Name = updated.Name,
                Description = updated.Description,
                StepId = updated.StepId,
                EntityIds = updated.EntityIds,
                UpdatedAt = updated.UpdatedAt,
                UpdatedBy = updated.UpdatedBy
            });

            stopwatch.Stop();
            _logger.LogInformationWithCorrelation("Successfully processed UpdateAssignmentCommand. Id: {Id}, Duration: {Duration}ms",
                updated.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new UpdateAssignmentCommandResponse
            {
                Success = true,
                Message = "Assignment entity updated successfully"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing UpdateAssignmentCommand. Id: {Id}, Duration: {Duration}ms",
                command.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new UpdateAssignmentCommandResponse
            {
                Success = false,
                Message = $"Failed to update Assignment entity: {ex.Message}"
            });
        }
    }
}

public class UpdateAssignmentCommandResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
