using System.Diagnostics;
using Manager.Assignment.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.Entities;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.Assignment.Consumers;

public class CreateAssignmentCommandConsumer : IConsumer<CreateAssignmentCommand>
{
    private readonly IAssignmentEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CreateAssignmentCommandConsumer> _logger;

    public CreateAssignmentCommandConsumer(
        IAssignmentEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<CreateAssignmentCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CreateAssignmentCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing CreateAssignmentCommand. Version: {Version}, Name: {Name}, StepId: {StepId}, EntityIds count: {EntityIdsCount}, RequestedBy: {RequestedBy}",
            command.Version, command.Name, command.StepId, command.EntityIds?.Count ?? 0, command.RequestedBy);

        try
        {
            var entity = new AssignmentEntity
            {
                Version = command.Version,
                Name = command.Name,
                Description = command.Description,
                StepId = command.StepId,
                EntityIds = command.EntityIds ?? new List<Guid>(),
                CreatedBy = command.RequestedBy
            };

            var created = await _repository.CreateAsync(entity);

            await _publishEndpoint.Publish(new AssignmentCreatedEvent
            {
                Id = created.Id,
                Version = created.Version,
                Name = created.Name,
                Description = created.Description,
                StepId = created.StepId,
                EntityIds = created.EntityIds,
                CreatedAt = created.CreatedAt,
                CreatedBy = created.CreatedBy
            });

            stopwatch.Stop();
            _logger.LogInformationWithCorrelation("Successfully processed CreateAssignmentCommand. Id: {Id}, Duration: {Duration}ms",
                created.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new CreateAssignmentCommandResponse
            {
                Success = true,
                Id = created.Id,
                Message = "Assignment entity created successfully"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing CreateAssignmentCommand. Version: {Version}, Name: {Name}, StepId: {StepId}, EntityIds count: {EntityIdsCount}, Duration: {Duration}ms",
                command.Version, command.Name, command.StepId, command.EntityIds?.Count ?? 0, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new CreateAssignmentCommandResponse
            {
                Success = false,
                Id = Guid.Empty,
                Message = $"Failed to create Assignment entity: {ex.Message}"
            });
        }
    }
}

public class CreateAssignmentCommandResponse
{
    public bool Success { get; set; }
    public Guid Id { get; set; }
    public string Message { get; set; } = string.Empty;
}
