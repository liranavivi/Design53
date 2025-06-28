using System.Diagnostics;
using Manager.Schema.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.MassTransit.Commands;

namespace Manager.Schema.Consumers;

public class GetSchemaDefinitionQueryConsumer : IConsumer<GetSchemaDefinitionQuery>
{
    private readonly ISchemaEntityRepository _repository;
    private readonly ILogger<GetSchemaDefinitionQueryConsumer> _logger;

    public GetSchemaDefinitionQueryConsumer(
        ISchemaEntityRepository repository,
        ILogger<GetSchemaDefinitionQueryConsumer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetSchemaDefinitionQuery> context)
    {
        var query = context.Message;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformationWithCorrelation("Processing GetSchemaDefinitionQuery. SchemaId: {SchemaId}, RequestedBy: {RequestedBy}",
            query.SchemaId, query.RequestedBy);

        try
        {
            var entity = await _repository.GetByIdAsync(query.SchemaId);

            stopwatch.Stop();

            if (entity != null)
            {
                _logger.LogInformationWithCorrelation("Successfully processed GetSchemaDefinitionQuery. Found Schema Id: {Id}, Definition length: {DefinitionLength}, Duration: {Duration}ms",
                    entity.Id, entity.Definition?.Length ?? 0, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetSchemaDefinitionQueryResponse
                {
                    Success = true,
                    Definition = entity.Definition,
                    Message = "Schema definition retrieved successfully"
                });
            }
            else
            {
                _logger.LogWarningWithCorrelation("Schema entity not found. SchemaId: {SchemaId}, Duration: {Duration}ms",
                    query.SchemaId, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetSchemaDefinitionQueryResponse
                {
                    Success = false,
                    Definition = null,
                    Message = $"Schema entity with ID {query.SchemaId} not found"
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing GetSchemaDefinitionQuery. SchemaId: {SchemaId}, Duration: {Duration}ms",
                query.SchemaId, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new GetSchemaDefinitionQueryResponse
            {
                Success = false,
                Definition = null,
                Message = $"Error retrieving Schema definition: {ex.Message}"
            });
        }
    }
}
