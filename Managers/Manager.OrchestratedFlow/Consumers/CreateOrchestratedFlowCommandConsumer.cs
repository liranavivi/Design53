using System.Diagnostics;
using Manager.OrchestratedFlow.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.Entities;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.OrchestratedFlow.Consumers;

public class CreateOrchestratedFlowCommandConsumer : IConsumer<CreateOrchestratedFlowCommand>
{
    private readonly IOrchestratedFlowEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CreateOrchestratedFlowCommandConsumer> _logger;

    public CreateOrchestratedFlowCommandConsumer(
        IOrchestratedFlowEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<CreateOrchestratedFlowCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CreateOrchestratedFlowCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing CreateOrchestratedFlowCommand. Version: {Version}, Name: {Name}, WorkflowId: {WorkflowId}, AssignmentIds: {AssignmentIds}, RequestedBy: {RequestedBy}",
            command.Version, command.Name, command.WorkflowId, string.Join(",", command.AssignmentIds), command.RequestedBy);

        try
        {
            var entity = new OrchestratedFlowEntity
            {
                Version = command.Version,
                Name = command.Name,
                Description = command.Description,
                WorkflowId = command.WorkflowId,
                AssignmentIds = command.AssignmentIds,
                CreatedBy = command.RequestedBy
            };

            var created = await _repository.CreateAsync(entity);

            await _publishEndpoint.Publish(new OrchestratedFlowCreatedEvent
            {
                Id = created.Id,
                Version = created.Version,
                Name = created.Name,
                Description = created.Description,
                WorkflowId = created.WorkflowId,
                AssignmentIds = created.AssignmentIds,
                CreatedAt = created.CreatedAt,
                CreatedBy = created.CreatedBy
            });

            stopwatch.Stop();
            _logger.LogInformationWithCorrelation("Successfully processed CreateOrchestratedFlowCommand. Id: {Id}, Duration: {Duration}ms",
                created.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new CreateOrchestratedFlowCommandResponse
            {
                Success = true,
                Id = created.Id,
                Message = "OrchestratedFlow entity created successfully"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing CreateOrchestratedFlowCommand. Version: {Version}, Name: {Name}, WorkflowId: {WorkflowId}, AssignmentIds: {AssignmentIds}, Duration: {Duration}ms",
                command.Version, command.Name, command.WorkflowId, string.Join(",", command.AssignmentIds), stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new CreateOrchestratedFlowCommandResponse
            {
                Success = false,
                Id = Guid.Empty,
                Message = $"Failed to create OrchestratedFlow entity: {ex.Message}"
            });
        }
    }
}

public class CreateOrchestratedFlowCommandResponse
{
    public bool Success { get; set; }
    public Guid Id { get; set; }
    public string Message { get; set; } = string.Empty;
}
