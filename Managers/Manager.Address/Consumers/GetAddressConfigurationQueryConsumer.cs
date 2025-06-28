using System.Diagnostics;
using Manager.Address.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.MassTransit.Commands;

namespace Manager.Address.Consumers;

public class GetAddressPayloadQueryConsumer : IConsumer<GetAddressPayloadQuery>
{
    private readonly IAddressEntityRepository _repository;
    private readonly ILogger<GetAddressPayloadQueryConsumer> _logger;

    public GetAddressPayloadQueryConsumer(
        IAddressEntityRepository repository,
        ILogger<GetAddressPayloadQueryConsumer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetAddressPayloadQuery> context)
    {
        var query = context.Message;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformationWithCorrelation("Processing GetAddressPayloadQuery. AddressId: {AddressId}, RequestedBy: {RequestedBy}",
            query.AddressId, query.RequestedBy);

        try
        {
            var entity = await _repository.GetByIdAsync(query.AddressId);

            stopwatch.Stop();

            if (entity != null)
            {
                _logger.LogInformationWithCorrelation("Successfully processed GetAddressPayloadQuery. Found Address Id: {Id}, Payload length: {PayloadLength}, Duration: {Duration}ms",
                    entity.Id, entity.Payload?.Length ?? 0, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetAddressPayloadQueryResponse
                {
                    Success = true,
                    Payload = entity.Payload,
                    Message = "Address payload retrieved successfully"
                });
            }
            else
            {
                _logger.LogWarningWithCorrelation("Address entity not found. AddressId: {AddressId}, Duration: {Duration}ms",
                    query.AddressId, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetAddressPayloadQueryResponse
                {
                    Success = false,
                    Payload = string.Empty,
                    Message = $"Address entity with ID {query.AddressId} not found"
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing GetAddressPayloadQuery. AddressId: {AddressId}, Duration: {Duration}ms",
                query.AddressId, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new GetAddressPayloadQueryResponse
            {
                Success = false,
                Payload = string.Empty,
                Message = $"Error retrieving Address payload: {ex.Message}"
            });
        }
    }
}
