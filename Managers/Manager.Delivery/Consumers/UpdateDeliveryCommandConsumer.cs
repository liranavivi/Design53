using System.Diagnostics;
using Manager.Delivery.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.Entities;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.Delivery.Consumers;

public class UpdateDeliveryCommandConsumer : IConsumer<UpdateDeliveryCommand>
{
    private readonly IDeliveryEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<UpdateDeliveryCommandConsumer> _logger;

    public UpdateDeliveryCommandConsumer(
        IDeliveryEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<UpdateDeliveryCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UpdateDeliveryCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing UpdateDeliveryCommand. Id: {Id}, Version: {Version}, Name: {Name}, Payload: {Payload}, RequestedBy: {RequestedBy}",
            command.Id, command.Version, command.Name, command.Payload, command.RequestedBy);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(command.Id);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Delivery entity not found for update. Id: {Id}", command.Id);
                await context.RespondAsync(new UpdateDeliveryCommandResponse
                {
                    Success = false,
                    Message = $"Delivery entity with ID {command.Id} not found"
                });
                return;
            }

            var entity = new DeliveryEntity
            {
                Id = command.Id,
                Version = command.Version,
                Name = command.Name,
                Description = command.Description,
                Payload = command.Payload,
                SchemaId = command.SchemaId,
                UpdatedBy = command.RequestedBy,
                CreatedAt = existingEntity.CreatedAt,
                CreatedBy = existingEntity.CreatedBy
            };

            var updated = await _repository.UpdateAsync(entity);

            await _publishEndpoint.Publish(new DeliveryUpdatedEvent
            {
                Id = updated.Id,
                Version = updated.Version,
                Name = updated.Name,
                Description = updated.Description,
                Payload = updated.Payload,
                SchemaId = updated.SchemaId,
                UpdatedAt = updated.UpdatedAt,
                UpdatedBy = updated.UpdatedBy
            });

            stopwatch.Stop();
            _logger.LogInformationWithCorrelation("Successfully processed UpdateDeliveryCommand. Id: {Id}, Duration: {Duration}ms",
                updated.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new UpdateDeliveryCommandResponse
            {
                Success = true,
                Message = "Delivery entity updated successfully"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing UpdateDeliveryCommand. Id: {Id}, Duration: {Duration}ms",
                command.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new UpdateDeliveryCommandResponse
            {
                Success = false,
                Message = $"Failed to update Delivery entity: {ex.Message}"
            });
        }
    }
}

public class UpdateDeliveryCommandResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
