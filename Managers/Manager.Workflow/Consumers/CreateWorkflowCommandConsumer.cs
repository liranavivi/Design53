using System.Diagnostics;
using Manager.Workflow.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.Entities;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.Workflow.Consumers;

public class CreateWorkflowCommandConsumer : IConsumer<CreateWorkflowCommand>
{
    private readonly IWorkflowEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CreateWorkflowCommandConsumer> _logger;

    public CreateWorkflowCommandConsumer(
        IWorkflowEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<CreateWorkflowCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CreateWorkflowCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing CreateWorkflowCommand. Version: {Version}, Name: {Name}, StepIds: {StepIds}, RequestedBy: {RequestedBy}",
            command.Version, command.Name, string.Join(",", command.StepIds), command.RequestedBy);

        try
        {
            var entity = new WorkflowEntity
            {
                Version = command.Version,
                Name = command.Name,
                Description = command.Description,
                StepIds = command.StepIds,
                CreatedBy = command.RequestedBy
            };

            var created = await _repository.CreateAsync(entity);

            await _publishEndpoint.Publish(new WorkflowCreatedEvent
            {
                Id = created.Id,
                Version = created.Version,
                Name = created.Name,
                Description = created.Description,
                StepIds = created.StepIds,
                CreatedAt = created.CreatedAt,
                CreatedBy = created.CreatedBy
            });

            stopwatch.Stop();
            _logger.LogInformationWithCorrelation("Successfully processed CreateWorkflowCommand. Id: {Id}, Duration: {Duration}ms",
                created.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new CreateWorkflowCommandResponse
            {
                Success = true,
                Id = created.Id,
                Message = "Workflow entity created successfully"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing CreateWorkflowCommand. Version: {Version}, Name: {Name}, StepIds: {StepIds}, Duration: {Duration}ms",
                command.Version, command.Name, string.Join(",", command.StepIds), stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new CreateWorkflowCommandResponse
            {
                Success = false,
                Id = Guid.Empty,
                Message = $"Failed to create Workflow entity: {ex.Message}"
            });
        }
    }
}

public class CreateWorkflowCommandResponse
{
    public bool Success { get; set; }
    public Guid Id { get; set; }
    public string Message { get; set; } = string.Empty;
}
