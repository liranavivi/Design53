using System.Diagnostics;
using Manager.Delivery.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.Entities;
using Shared.MassTransit.Commands;

namespace Manager.Delivery.Consumers;

public class GetDeliveryQueryConsumer : IConsumer<GetDeliveryQuery>
{
    private readonly IDeliveryEntityRepository _repository;
    private readonly ILogger<GetDeliveryQueryConsumer> _logger;

    public GetDeliveryQueryConsumer(
        IDeliveryEntityRepository repository,
        ILogger<GetDeliveryQueryConsumer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetDeliveryQuery> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var query = context.Message;

        _logger.LogInformationWithCorrelation("Processing GetDeliveryQuery. Id: {Id}, CompositeKey: {CompositeKey}",
            query.Id, query.CompositeKey);

        try
        {
            DeliveryEntity? entity = null;

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
                _logger.LogInformationWithCorrelation("Successfully processed GetDeliveryQuery. Found entity Id: {Id}, Duration: {Duration}ms",
                    entity.Id, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetDeliveryQueryResponse
                {
                    Success = true,
                    Entity = entity,
                    Message = "Delivery entity found"
                });
            }
            else
            {
                _logger.LogInformationWithCorrelation("Delivery entity not found. Id: {Id}, CompositeKey: {CompositeKey}, Duration: {Duration}ms",
                    query.Id, query.CompositeKey, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetDeliveryQueryResponse
                {
                    Success = false,
                    Entity = null,
                    Message = "Delivery entity not found"
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing GetDeliveryQuery. Id: {Id}, CompositeKey: {CompositeKey}, Duration: {Duration}ms",
                query.Id, query.CompositeKey, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new GetDeliveryQueryResponse
            {
                Success = false,
                Entity = null,
                Message = $"Error retrieving Delivery entity: {ex.Message}"
            });
        }
    }
}

public class GetDeliveryQueryResponse
{
    public bool Success { get; set; }
    public DeliveryEntity? Entity { get; set; }
    public string Message { get; set; } = string.Empty;
}
