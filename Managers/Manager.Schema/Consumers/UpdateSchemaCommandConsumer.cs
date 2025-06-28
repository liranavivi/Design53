using System.Diagnostics;
using Manager.Schema.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.Entities;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.Schema.Consumers;

public class UpdateSchemaCommandConsumer : IConsumer<UpdateSchemaCommand>
{
    private readonly ISchemaEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<UpdateSchemaCommandConsumer> _logger;

    public UpdateSchemaCommandConsumer(
        ISchemaEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<UpdateSchemaCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UpdateSchemaCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing UpdateSchemaCommand. Id: {Id}, Version: {Version}, Name: {Name}, Definition: {Definition}, RequestedBy: {RequestedBy}",
            command.Id, command.Version, command.Name, command.Definition, command.RequestedBy);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(command.Id);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Schema entity not found for update. Id: {Id}", command.Id);
                await context.RespondAsync(new UpdateSchemaCommandResponse
                {
                    Success = false,
                    Message = $"Schema entity with ID {command.Id} not found"
                });
                return;
            }

            var entity = new SchemaEntity
            {
                Id = command.Id,
                Version = command.Version,
                Name = command.Name,
                Description = command.Description,
                Definition = command.Definition,
                UpdatedBy = command.RequestedBy,
                CreatedAt = existingEntity.CreatedAt,
                CreatedBy = existingEntity.CreatedBy
            };

            var updated = await _repository.UpdateAsync(entity);

            await _publishEndpoint.Publish(new SchemaUpdatedEvent
            {
                Id = updated.Id,
                Version = updated.Version,
                Name = updated.Name,
                Description = updated.Description,
                Definition = updated.Definition,
                UpdatedAt = updated.UpdatedAt,
                UpdatedBy = updated.UpdatedBy
            });

            stopwatch.Stop();
            _logger.LogInformationWithCorrelation("Successfully processed UpdateSchemaCommand. Id: {Id}, Duration: {Duration}ms",
                updated.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new UpdateSchemaCommandResponse
            {
                Success = true,
                Message = "Schema entity updated successfully"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing UpdateSchemaCommand. Id: {Id}, Duration: {Duration}ms",
                command.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new UpdateSchemaCommandResponse
            {
                Success = false,
                Message = $"Failed to update Schema entity: {ex.Message}"
            });
        }
    }
}

public class UpdateSchemaCommandResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
