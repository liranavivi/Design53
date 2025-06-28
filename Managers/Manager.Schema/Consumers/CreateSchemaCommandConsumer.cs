using System.Diagnostics;
using Manager.Schema.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.Entities;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.Schema.Consumers;

public class CreateSchemaCommandConsumer : IConsumer<CreateSchemaCommand>
{
    private readonly ISchemaEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CreateSchemaCommandConsumer> _logger;

    public CreateSchemaCommandConsumer(
        ISchemaEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<CreateSchemaCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CreateSchemaCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing CreateSchemaCommand. Version: {Version}, Name: {Name}, Definition: {Definition}, RequestedBy: {RequestedBy}",
            command.Version, command.Name, command.Definition, command.RequestedBy);

        try
        {
            var entity = new SchemaEntity
            {
                Version = command.Version,
                Name = command.Name,
                Description = command.Description,
                Definition = command.Definition,
                CreatedBy = command.RequestedBy
            };

            var created = await _repository.CreateAsync(entity);

            await _publishEndpoint.Publish(new SchemaCreatedEvent
            {
                Id = created.Id,
                Version = created.Version,
                Name = created.Name,
                Description = created.Description,
                Definition = created.Definition,
                CreatedAt = created.CreatedAt,
                CreatedBy = created.CreatedBy
            });

            stopwatch.Stop();
            _logger.LogInformationWithCorrelation("Successfully processed CreateSchemaCommand. Id: {Id}, Duration: {Duration}ms",
                created.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new CreateSchemaCommandResponse
            {
                Success = true,
                Id = created.Id,
                Message = "Schema entity created successfully"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing CreateSchemaCommand. Version: {Version}, Name: {Name}, Definition: {Definition}, Duration: {Duration}ms",
                command.Version, command.Name, command.Definition, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new CreateSchemaCommandResponse
            {
                Success = false,
                Id = Guid.Empty,
                Message = $"Failed to create Schema entity: {ex.Message}"
            });
        }
    }
}

public class CreateSchemaCommandResponse
{
    public bool Success { get; set; }
    public Guid Id { get; set; }
    public string Message { get; set; } = string.Empty;
}
