using System.Diagnostics;
using Manager.Delivery.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.Entities;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.Delivery.Consumers;

public class CreateDeliveryCommandConsumer : IConsumer<CreateDeliveryCommand>
{
    private readonly IDeliveryEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CreateDeliveryCommandConsumer> _logger;

    public CreateDeliveryCommandConsumer(
        IDeliveryEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<CreateDeliveryCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CreateDeliveryCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing CreateDeliveryCommand. Version: {Version}, Name: {Name}, Payload: {Payload}, RequestedBy: {RequestedBy}",
            command.Version, command.Name, command.Payload, command.RequestedBy);

        try
        {
            var entity = new DeliveryEntity
            {
                Version = command.Version,
                Name = command.Name,
                Description = command.Description,
                Payload = command.Payload,
                SchemaId = command.SchemaId,
                CreatedBy = command.RequestedBy
            };

            var created = await _repository.CreateAsync(entity);

            await _publishEndpoint.Publish(new DeliveryCreatedEvent
            {
                Id = created.Id,
                Version = created.Version,
                Name = created.Name,
                Description = created.Description,
                Payload = created.Payload,
                SchemaId = created.SchemaId,
                CreatedAt = created.CreatedAt,
                CreatedBy = created.CreatedBy
            });

            stopwatch.Stop();
            _logger.LogInformationWithCorrelation("Successfully processed CreateDeliveryCommand. Id: {Id}, Duration: {Duration}ms",
                created.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new CreateDeliveryCommandResponse
            {
                Success = true,
                Id = created.Id,
                Message = "Delivery entity created successfully"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing CreateDeliveryCommand. Version: {Version}, Name: {Name}, Payload: {Payload}, Duration: {Duration}ms",
                command.Version, command.Name, command.Payload, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new CreateDeliveryCommandResponse
            {
                Success = false,
                Id = Guid.Empty,
                Message = $"Failed to create Delivery entity: {ex.Message}"
            });
        }
    }
}

public class CreateDeliveryCommandResponse
{
    public bool Success { get; set; }
    public Guid Id { get; set; }
    public string Message { get; set; } = string.Empty;
}
