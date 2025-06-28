using System.Diagnostics;
using Manager.Step.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.Entities;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.Step.Consumers;

public class UpdateStepCommandConsumer : IConsumer<UpdateStepCommand>
{
    private readonly IStepEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<UpdateStepCommandConsumer> _logger;

    public UpdateStepCommandConsumer(
        IStepEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<UpdateStepCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UpdateStepCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing UpdateStepCommand. Id: {Id}, Version: {Version}, Name: {Name}, ProcessorId: {ProcessorId}, NextStepIds: {NextStepIds}, EntryCondition: {EntryCondition}, RequestedBy: {RequestedBy}",
            command.Id, command.Version, command.Name, command.ProcessorId, string.Join(",", command.NextStepIds ?? new List<Guid>()), command.EntryCondition, command.RequestedBy);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(command.Id);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Step entity not found for update. Id: {Id}", command.Id);
                await context.RespondAsync(new UpdateStepCommandResponse
                {
                    Success = false,
                    Message = $"Step entity with ID {command.Id} not found"
                });
                return;
            }

            var entity = new StepEntity
            {
                Id = command.Id,
                Version = command.Version,
                Name = command.Name,
                Description = command.Description,
                ProcessorId = command.ProcessorId,
                NextStepIds = command.NextStepIds ?? new List<Guid>(),
                EntryCondition = command.EntryCondition,
                UpdatedBy = command.RequestedBy,
                CreatedAt = existingEntity.CreatedAt,
                CreatedBy = existingEntity.CreatedBy
            };

            var updated = await _repository.UpdateAsync(entity);

            await _publishEndpoint.Publish(new StepUpdatedEvent
            {
                Id = updated.Id,
                Version = updated.Version,
                Name = updated.Name,
                Description = updated.Description,
                ProcessorId = updated.ProcessorId,
                NextStepIds = updated.NextStepIds,
                EntryCondition = updated.EntryCondition,
                UpdatedAt = updated.UpdatedAt,
                UpdatedBy = updated.UpdatedBy
            });

            stopwatch.Stop();
            _logger.LogInformationWithCorrelation("Successfully processed UpdateStepCommand. Id: {Id}, Duration: {Duration}ms",
                updated.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new UpdateStepCommandResponse
            {
                Success = true,
                Message = "Step entity updated successfully"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing UpdateStepCommand. Id: {Id}, Duration: {Duration}ms",
                command.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new UpdateStepCommandResponse
            {
                Success = false,
                Message = $"Failed to update Step entity: {ex.Message}"
            });
        }
    }
}

public class UpdateStepCommandResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
