using System.Diagnostics;
using Manager.Workflow.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.Entities;
using Shared.MassTransit.Commands;

namespace Manager.Workflow.Consumers;
public class GetWorkflowQueryConsumer : IConsumer<GetWorkflowQuery>
{
    private readonly IWorkflowEntityRepository _repository;
    private readonly ILogger<GetWorkflowQueryConsumer> _logger;

    public GetWorkflowQueryConsumer(
        IWorkflowEntityRepository repository,
        ILogger<GetWorkflowQueryConsumer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetWorkflowQuery> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var query = context.Message;

        _logger.LogInformationWithCorrelation("Processing GetWorkflowQuery. Id: {Id}, CompositeKey: {CompositeKey}",
            query.Id, query.CompositeKey);

        try
        {
            WorkflowEntity? entity = null;

            if (query.Id.HasValue)
            {
                entity = await _repository.GetByIdAsync(query.Id.Value);
            }
            else if (!string.IsNullOrEmpty(query.CompositeKey))
            {
                entity = await _repository.GetByCompositeKeyAsync(query.CompositeKey);
            }

            stopwatch.Stop();

            if (entity != null)
            {
                _logger.LogInformationWithCorrelation("Successfully processed GetWorkflowQuery. Found entity Id: {Id}, Duration: {Duration}ms",
                    entity.Id, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetWorkflowQueryResponse
                {
                    Success = true,
                    Entity = entity,
                    Message = "Workflow entity found"
                });
            }
            else
            {
                _logger.LogInformationWithCorrelation("Workflow entity not found. Id: {Id}, CompositeKey: {CompositeKey}, Duration: {Duration}ms",
                    query.Id, query.CompositeKey, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetWorkflowQueryResponse
                {
                    Success = false,
                    Entity = null,
                    Message = "Workflow entity not found"
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing GetWorkflowQuery. Id: {Id}, CompositeKey: {CompositeKey}, Duration: {Duration}ms",
                query.Id, query.CompositeKey, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new GetWorkflowQueryResponse
            {
                Success = false,
                Entity = null,
                Message = $"Error retrieving Workflow entity: {ex.Message}"
            });
        }
    }
}

public class GetWorkflowQueryResponse
{
    public bool Success { get; set; }
    public WorkflowEntity? Entity { get; set; }
    public string Message { get; set; } = string.Empty;
}
