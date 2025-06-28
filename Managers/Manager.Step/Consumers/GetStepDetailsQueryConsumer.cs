using System.Diagnostics;
using Manager.Step.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.MassTransit.Commands;

namespace Manager.Step.Consumers;

public class GetStepDetailsQueryConsumer : IConsumer<GetStepDetailsQuery>
{
    private readonly IStepEntityRepository _repository;
    private readonly ILogger<GetStepDetailsQueryConsumer> _logger;

    public GetStepDetailsQueryConsumer(
        IStepEntityRepository repository,
        ILogger<GetStepDetailsQueryConsumer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetStepDetailsQuery> context)
    {
        var query = context.Message;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformationWithCorrelation("Processing GetStepDetailsQuery. StepId: {StepId}, RequestedBy: {RequestedBy}",
            query.StepId, query.RequestedBy);

        try
        {
            var entity = await _repository.GetByIdAsync(query.StepId);

            stopwatch.Stop();

            if (entity != null)
            {
                _logger.LogInformationWithCorrelation("Successfully processed GetStepDetailsQuery. Found Step Id: {Id}, ProcessorId: {ProcessorId}, NextStepIds: {NextStepIds}, EntryCondition: {EntryCondition}, Duration: {Duration}ms",
                    entity.Id, entity.ProcessorId, string.Join(",", entity.NextStepIds), entity.EntryCondition, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetStepDetailsQueryResponse
                {
                    Success = true,
                    Entity = entity,
                    Message = "Step details retrieved successfully"
                });
            }
            else
            {
                _logger.LogWarningWithCorrelation("Step entity not found. StepId: {StepId}, Duration: {Duration}ms",
                    query.StepId, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetStepDetailsQueryResponse
                {
                    Success = false,
                    Entity = null,
                    Message = $"Step entity with ID {query.StepId} not found"
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing GetStepDetailsQuery. StepId: {StepId}, Duration: {Duration}ms",
                query.StepId, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new GetStepDetailsQueryResponse
            {
                Success = false,
                Entity = null,
                Message = $"Error retrieving Step details: {ex.Message}"
            });
        }
    }
}
