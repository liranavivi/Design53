using System.Diagnostics;
using Manager.Step.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.Step.Consumers;
public class DeleteStepCommandConsumer : IConsumer<DeleteStepCommand>
{
    private readonly IStepEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<DeleteStepCommandConsumer> _logger;

    public DeleteStepCommandConsumer(
        IStepEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<DeleteStepCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DeleteStepCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing DeleteStepCommand. Id: {Id}, RequestedBy: {RequestedBy}",
            command.Id, command.RequestedBy);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(command.Id);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Step entity not found for deletion. Id: {Id}", command.Id);
                await context.RespondAsync(new DeleteStepCommandResponse
                {
                    Success = false,
                    Message = $"Step entity with ID {command.Id} not found"
                });
                return;
            }

            var success = await _repository.DeleteAsync(command.Id);

            if (success)
            {
                await _publishEndpoint.Publish(new StepDeletedEvent
                {
                    Id = command.Id,
                    DeletedAt = DateTime.UtcNow,
                    DeletedBy = command.RequestedBy
                });

                stopwatch.Stop();
                _logger.LogInformationWithCorrelation("Successfully processed DeleteStepCommand. Id: {Id}, Duration: {Duration}ms",
                    command.Id, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new DeleteStepCommandResponse
                {
                    Success = true,
                    Message = "Step entity deleted successfully"
                });
            }
            else
            {
                stopwatch.Stop();
                _logger.LogWarningWithCorrelation("Failed to delete Step entity. Id: {Id}, Duration: {Duration}ms",
                    command.Id, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new DeleteStepCommandResponse
                {
                    Success = false,
                    Message = "Failed to delete Step entity"
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing DeleteStepCommand. Id: {Id}, Duration: {Duration}ms",
                command.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new DeleteStepCommandResponse
            {
                Success = false,
                Message = $"Failed to delete Step entity: {ex.Message}"
            });
        }
    }
}

public class DeleteStepCommandResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
