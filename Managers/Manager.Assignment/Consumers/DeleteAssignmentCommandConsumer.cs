using System.Diagnostics;
using Manager.Assignment.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.Assignment.Consumers;

public class DeleteAssignmentCommandConsumer : IConsumer<DeleteAssignmentCommand>
{
    private readonly IAssignmentEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<DeleteAssignmentCommandConsumer> _logger;

    public DeleteAssignmentCommandConsumer(
        IAssignmentEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<DeleteAssignmentCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DeleteAssignmentCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing DeleteAssignmentCommand. Id: {Id}, RequestedBy: {RequestedBy}",
            command.Id, command.RequestedBy);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(command.Id);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Assignment entity not found for deletion. Id: {Id}", command.Id);
                await context.RespondAsync(new DeleteAssignmentCommandResponse
                {
                    Success = false,
                    Message = $"Assignment entity with ID {command.Id} not found"
                });
                return;
            }

            var success = await _repository.DeleteAsync(command.Id);

            if (success)
            {
                await _publishEndpoint.Publish(new AssignmentDeletedEvent
                {
                    Id = command.Id,
                    DeletedAt = DateTime.UtcNow,
                    DeletedBy = command.RequestedBy
                });

                stopwatch.Stop();
                _logger.LogInformationWithCorrelation("Successfully processed DeleteAssignmentCommand. Id: {Id}, Duration: {Duration}ms",
                    command.Id, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new DeleteAssignmentCommandResponse
                {
                    Success = true,
                    Message = "Assignment entity deleted successfully"
                });
            }
            else
            {
                stopwatch.Stop();
                _logger.LogWarningWithCorrelation("Failed to delete Assignment entity. Id: {Id}, Duration: {Duration}ms",
                    command.Id, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new DeleteAssignmentCommandResponse
                {
                    Success = false,
                    Message = "Failed to delete Assignment entity"
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing DeleteAssignmentCommand. Id: {Id}, Duration: {Duration}ms",
                command.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new DeleteAssignmentCommandResponse
            {
                Success = false,
                Message = $"Failed to delete Assignment entity: {ex.Message}"
            });
        }
    }
}

public class DeleteAssignmentCommandResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
