using System.Diagnostics;
using Manager.OrchestratedFlow.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.Entities;
using Shared.MassTransit.Commands;

namespace Manager.OrchestratedFlow.Consumers;

public class GetOrchestratedFlowQueryConsumer : IConsumer<GetOrchestratedFlowQuery>
{
    private readonly IOrchestratedFlowEntityRepository _repository;
    private readonly ILogger<GetOrchestratedFlowQueryConsumer> _logger;

    public GetOrchestratedFlowQueryConsumer(
        IOrchestratedFlowEntityRepository repository,
        ILogger<GetOrchestratedFlowQueryConsumer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetOrchestratedFlowQuery> context)
    {
        var stopwatch = Stopwatch.StartNew();
        var query = context.Message;

        _logger.LogInformationWithCorrelation("Processing GetOrchestratedFlowQuery. Id: {Id}, CompositeKey: {CompositeKey}",
            query.Id, query.CompositeKey);

        try
        {
            OrchestratedFlowEntity? entity = null;

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
                _logger.LogInformationWithCorrelation("Successfully processed GetOrchestratedFlowQuery. Found entity Id: {Id}, Duration: {Duration}ms",
                    entity.Id, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetOrchestratedFlowQueryResponse
                {
                    Success = true,
                    Entity = entity,
                    Message = "OrchestratedFlow entity found"
                });
            }
            else
            {
                _logger.LogInformationWithCorrelation("OrchestratedFlow entity not found. Id: {Id}, CompositeKey: {CompositeKey}, Duration: {Duration}ms",
                    query.Id, query.CompositeKey, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetOrchestratedFlowQueryResponse
                {
                    Success = false,
                    Entity = null,
                    Message = "OrchestratedFlow entity not found"
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing GetOrchestratedFlowQuery. Id: {Id}, CompositeKey: {CompositeKey}, Duration: {Duration}ms",
                query.Id, query.CompositeKey, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new GetOrchestratedFlowQueryResponse
            {
                Success = false,
                Entity = null,
                Message = $"Error retrieving OrchestratedFlow entity: {ex.Message}"
            });
        }
    }
}

public class GetOrchestratedFlowQueryResponse
{
    public bool Success { get; set; }
    public OrchestratedFlowEntity? Entity { get; set; }
    public string Message { get; set; } = string.Empty;
}
