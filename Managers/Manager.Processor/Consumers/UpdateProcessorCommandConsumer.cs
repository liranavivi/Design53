using System.Diagnostics;
using Manager.Processor.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.Entities;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.Processor.Consumers;

public class UpdateProcessorCommandConsumer : IConsumer<UpdateProcessorCommand>
{
    private readonly IProcessorEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<UpdateProcessorCommandConsumer> _logger;

    public UpdateProcessorCommandConsumer(
        IProcessorEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<UpdateProcessorCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UpdateProcessorCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing UpdateProcessorCommand. Id: {Id}, Version: {Version}, Name: {Name}, InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}, RequestedBy: {RequestedBy}",
            command.Id, command.Version, command.Name, command.InputSchemaId, command.OutputSchemaId, command.RequestedBy);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(command.Id);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Processor entity not found for update. Id: {Id}", command.Id);
                await context.RespondAsync(new UpdateProcessorCommandResponse
                {
                    Success = false,
                    Message = $"Processor entity with ID {command.Id} not found"
                });
                return;
            }

            var entity = new ProcessorEntity
            {
                Id = command.Id,
                Version = command.Version,
                Name = command.Name,
                Description = command.Description,
                InputSchemaId = command.InputSchemaId,
                OutputSchemaId = command.OutputSchemaId,
                UpdatedBy = command.RequestedBy,
                CreatedAt = existingEntity.CreatedAt,
                CreatedBy = existingEntity.CreatedBy
            };

            var updated = await _repository.UpdateAsync(entity);

            await _publishEndpoint.Publish(new ProcessorUpdatedEvent
            {
                Id = updated.Id,
                Version = updated.Version,
                Name = updated.Name,
                Description = updated.Description,
                InputSchemaId = updated.InputSchemaId,
                OutputSchemaId = updated.OutputSchemaId,
                UpdatedAt = updated.UpdatedAt,
                UpdatedBy = updated.UpdatedBy
            });

            stopwatch.Stop();
            _logger.LogInformationWithCorrelation("Successfully processed UpdateProcessorCommand. Id: {Id}, Duration: {Duration}ms",
                updated.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new UpdateProcessorCommandResponse
            {
                Success = true,
                Message = "Processor entity updated successfully"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing UpdateProcessorCommand. Id: {Id}, Duration: {Duration}ms",
                command.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new UpdateProcessorCommandResponse
            {
                Success = false,
                Message = $"Failed to update Processor entity: {ex.Message}"
            });
        }
    }
}

public class UpdateProcessorCommandResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
