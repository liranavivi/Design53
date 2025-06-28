using System.Diagnostics;
using Manager.Address.Repositories;
using MassTransit;
using Shared.Entities;
using Shared.Correlation;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.Address.Consumers;

public class UpdateAddressCommandConsumer : IConsumer<UpdateAddressCommand>
{
    private readonly IAddressEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<UpdateAddressCommandConsumer> _logger;

    public UpdateAddressCommandConsumer(
        IAddressEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<UpdateAddressCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UpdateAddressCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing UpdateAddressCommand. Id: {Id}, Version: {Version}, Name: {Name}, ConnectionString: {ConnectionString}, RequestedBy: {RequestedBy}",
            command.Id, command.Version, command.Name, command.ConnectionString, command.RequestedBy);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(command.Id);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Address entity not found for update. Id: {Id}", command.Id);
                await context.RespondAsync(new UpdateAddressCommandResponse
                {
                    Success = false,
                    Message = $"Address entity with ID {command.Id} not found"
                });
                return;
            }

            var entity = new AddressEntity
            {
                Id = command.Id,
                Version = command.Version,
                Name = command.Name,
                Description = command.Description,
                ConnectionString = command.ConnectionString,
                Payload = command.Payload,
                SchemaId = command.SchemaId,
                UpdatedBy = command.RequestedBy,
                CreatedAt = existingEntity.CreatedAt,
                CreatedBy = existingEntity.CreatedBy
            };

            var updated = await _repository.UpdateAsync(entity);

            await _publishEndpoint.Publish(new AddressUpdatedEvent
            {
                Id = updated.Id,
                Version = updated.Version,
                Name = updated.Name,
                Description = updated.Description,
                ConnectionString = updated.ConnectionString,
                Payload = updated.Payload,
                SchemaId = updated.SchemaId,
                UpdatedAt = updated.UpdatedAt,
                UpdatedBy = updated.UpdatedBy
            });

            stopwatch.Stop();
            _logger.LogInformationWithCorrelation("Successfully processed UpdateAddressCommand. Id: {Id}, Duration: {Duration}ms",
                updated.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new UpdateAddressCommandResponse
            {
                Success = true,
                Message = "Address entity updated successfully"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing UpdateAddressCommand. Id: {Id}, Duration: {Duration}ms",
                command.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new UpdateAddressCommandResponse
            {
                Success = false,
                Message = $"Failed to update Address entity: {ex.Message}"
            });
        }
    }
}

public class UpdateAddressCommandResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
