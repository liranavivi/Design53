using System.Diagnostics;
using Manager.Address.Repositories;
using MassTransit;
using Shared.Entities;
using Shared.Correlation;
using Shared.MassTransit.Commands;

namespace Manager.Address.Consumers;

public class GetAddressQueryConsumer : IConsumer<GetAddressQuery>
{
    private readonly IAddressEntityRepository _repository;
    private readonly ILogger<GetAddressQueryConsumer> _logger;

    public GetAddressQueryConsumer(
        IAddressEntityRepository repository,
        ILogger<GetAddressQueryConsumer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetAddressQuery> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var query = context.Message;

        _logger.LogInformationWithCorrelation("Processing GetAddressQuery. Id: {Id}, CompositeKey: {CompositeKey}",
            query.Id, query.CompositeKey);

        try
        {
            AddressEntity? entity = null;

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
                _logger.LogInformationWithCorrelation("Successfully processed GetAddressQuery. Found entity Id: {Id}, Duration: {Duration}ms",
                    entity.Id, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetAddressQueryResponse
                {
                    Success = true,
                    Entity = entity,
                    Message = "Address entity found"
                });
            }
            else
            {
                _logger.LogInformationWithCorrelation("Address entity not found. Id: {Id}, CompositeKey: {CompositeKey}, Duration: {Duration}ms",
                    query.Id, query.CompositeKey, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetAddressQueryResponse
                {
                    Success = false,
                    Entity = null,
                    Message = "Address entity not found"
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing GetAddressQuery. Id: {Id}, CompositeKey: {CompositeKey}, Duration: {Duration}ms",
                query.Id, query.CompositeKey, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new GetAddressQueryResponse
            {
                Success = false,
                Entity = null,
                Message = $"Error retrieving Address entity: {ex.Message}"
            });
        }
    }
}

public class GetAddressQueryResponse
{
    public bool Success { get; set; }
    public AddressEntity? Entity { get; set; }
    public string Message { get; set; } = string.Empty;
}
