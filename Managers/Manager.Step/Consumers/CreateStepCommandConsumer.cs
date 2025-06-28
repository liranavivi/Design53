using System.Diagnostics;
using Manager.Step.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.Entities;
using Shared.MassTransit.Commands;
using Shared.MassTransit.Events;

namespace Manager.Step.Consumers;

public class CreateStepCommandConsumer : IConsumer<CreateStepCommand>
{
    private readonly IStepEntityRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CreateStepCommandConsumer> _logger;

    public CreateStepCommandConsumer(
        IStepEntityRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<CreateStepCommandConsumer> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CreateStepCommand> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var command = context.Message;

        _logger.LogInformationWithCorrelation("Processing CreateStepCommand. Version: {Version}, Name: {Name}, ProcessorId: {ProcessorId}, NextStepIds: {NextStepIds}, EntryCondition: {EntryCondition}, RequestedBy: {RequestedBy}",
            command.Version, command.Name, command.ProcessorId, string.Join(",", command.NextStepIds ?? new List<Guid>()), command.EntryCondition, command.RequestedBy);

        try
        {
            var entity = new StepEntity
            {
                Version = command.Version,
                Name = command.Name,
                Description = command.Description,
                ProcessorId = command.ProcessorId,
                NextStepIds = command.NextStepIds ?? new List<Guid>(),
                EntryCondition = command.EntryCondition,
                CreatedBy = command.RequestedBy
            };

            var created = await _repository.CreateAsync(entity);

            await _publishEndpoint.Publish(new StepCreatedEvent
            {
                Id = created.Id,
                Version = created.Version,
                Name = created.Name,
                Description = created.Description,
                ProcessorId = created.ProcessorId,
                NextStepIds = created.NextStepIds,
                EntryCondition = created.EntryCondition,
                CreatedAt = created.CreatedAt,
                CreatedBy = created.CreatedBy
            });

            stopwatch.Stop();
            _logger.LogInformationWithCorrelation("Successfully processed CreateStepCommand. Id: {Id}, Duration: {Duration}ms",
                created.Id, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new CreateStepCommandResponse
            {
                Success = true,
                Id = created.Id,
                Message = "Step entity created successfully"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing CreateStepCommand. Version: {Version}, Name: {Name}, ProcessorId: {ProcessorId}, NextStepIds: {NextStepIds}, EntryCondition: {EntryCondition}, Duration: {Duration}ms",
                command.Version, command.Name, command.ProcessorId, string.Join(",", command.NextStepIds ?? new List<Guid>()), command.EntryCondition, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new CreateStepCommandResponse
            {
                Success = false,
                Id = Guid.Empty,
                Message = $"Failed to create Step entity: {ex.Message}"
            });
        }
    }
}

public class CreateStepCommandResponse
{
    public bool Success { get; set; }
    public Guid Id { get; set; }
    public string Message { get; set; } = string.Empty;
}
