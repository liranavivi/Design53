using System.Diagnostics;
using Manager.Step.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.Entities;
using Shared.MassTransit.Commands;

namespace Manager.Step.Consumers;

public class GetStepQueryConsumer : IConsumer<GetStepQuery>
{
    private readonly IStepEntityRepository _repository;
    private readonly ILogger<GetStepQueryConsumer> _logger;

    public GetStepQueryConsumer(
        IStepEntityRepository repository,
        ILogger<GetStepQueryConsumer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetStepQuery> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var query = context.Message;

        _logger.LogInformationWithCorrelation("Processing GetStepQuery. Id: {Id}, CompositeKey: {CompositeKey}",
            query.Id, query.CompositeKey);

        try
        {
            StepEntity? entity = null;

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
                _logger.LogInformationWithCorrelation("Successfully processed GetStepQuery. Found entity Id: {Id}, Duration: {Duration}ms",
                    entity.Id, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetStepQueryResponse
                {
                    Success = true,
                    Entity = entity,
                    Message = "Step entity found"
                });
            }
            else
            {
                _logger.LogInformationWithCorrelation("Step entity not found. Id: {Id}, CompositeKey: {CompositeKey}, Duration: {Duration}ms",
                    query.Id, query.CompositeKey, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetStepQueryResponse
                {
                    Success = false,
                    Entity = null,
                    Message = "Step entity not found"
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing GetStepQuery. Id: {Id}, CompositeKey: {CompositeKey}, Duration: {Duration}ms",
                query.Id, query.CompositeKey, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new GetStepQueryResponse
            {
                Success = false,
                Entity = null,
                Message = $"Error retrieving Step entity: {ex.Message}"
            });
        }
    }
}

public class GetStepQueryResponse
{
    public bool Success { get; set; }
    public StepEntity? Entity { get; set; }
    public string Message { get; set; } = string.Empty;
}
