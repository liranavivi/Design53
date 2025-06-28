using System.Diagnostics;
using Manager.Processor.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.Entities;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.Processor.Consumers;

public class CreateProcessorCommandConsumer : IConsumer<CreateProcessorCommand>
{
    private readonly IProcessorEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CreateProcessorCommandConsumer> _logger;

    public CreateProcessorCommandConsumer(
        IProcessorEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<CreateProcessorCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CreateProcessorCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing CreateProcessorCommand. Version: {Version}, Name: {Name}, InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}, ImplementationHash: {ImplementationHash}, RequestedBy: {RequestedBy}",
            command.Version, command.Name, command.InputSchemaId, command.OutputSchemaId, command.ImplementationHash, command.RequestedBy);

        try
        {
            var entity = new ProcessorEntity
            {
                Version = command.Version,
                Name = command.Name,
                Description = command.Description,
                InputSchemaId = command.InputSchemaId,
                OutputSchemaId = command.OutputSchemaId,
                ImplementationHash = command.ImplementationHash,
                CreatedBy = command.RequestedBy
            };

            var created = await _repository.CreateAsync(entity);

            await _publishEndpoint.Publish(new ProcessorCreatedEvent
            {
                Id = created.Id,
                Version = created.Version,
                Name = created.Name,
                Description = created.Description,
                InputSchemaId = created.InputSchemaId,
                OutputSchemaId = created.OutputSchemaId,
                CreatedAt = created.CreatedAt,
                CreatedBy = created.CreatedBy
            });

            stopwatch.Stop();
            _logger.LogInformationWithCorrelation("Successfully processed CreateProcessorCommand. Id: {Id}, Duration: {Duration}ms",
                created.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new CreateProcessorCommandResponse
            {
                Success = true,
                Id = created.Id,
                Message = "Processor entity created successfully"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing CreateProcessorCommand. Version: {Version}, Name: {Name}, InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}, ImplementationHash: {ImplementationHash}, Duration: {Duration}ms",
                command.Version, command.Name, command.InputSchemaId, command.OutputSchemaId, command.ImplementationHash, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new CreateProcessorCommandResponse
            {
                Success = false,
                Id = Guid.Empty,
                Message = $"Failed to create Processor entity: {ex.Message}"
            });
        }
    }
}

public class CreateProcessorCommandResponse
{
    public bool Success { get; set; }
    public Guid Id { get; set; }
    public string Message { get; set; } = string.Empty;
}
