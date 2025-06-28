using System.Diagnostics;
using Manager.Processor.Repositories;
using MassTransit;
using Shared.Correlation;
using Shared.MassTransit.Commands;

namespace Manager.Processor.Consumers;

public class GetProcessorSchemaQueryConsumer : IConsumer<GetProcessorSchemaQuery>
{
    private readonly IProcessorEntityRepository _repository;
    private readonly ILogger<GetProcessorSchemaQueryConsumer> _logger;

    public GetProcessorSchemaQueryConsumer(
        IProcessorEntityRepository repository,
        ILogger<GetProcessorSchemaQueryConsumer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetProcessorSchemaQuery> context)
    {
        var query = context.Message;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformationWithCorrelation("Processing GetProcessorSchemaQuery. ProcessorId: {ProcessorId}, RequestedBy: {RequestedBy}",
            query.ProcessorId, query.RequestedBy);

        try
        {
            var entity = await _repository.GetByIdAsync(query.ProcessorId);

            stopwatch.Stop();

            if (entity != null)
            {
                _logger.LogInformationWithCorrelation("Successfully processed GetProcessorSchemaQuery. Found Processor Id: {Id}, InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}, Duration: {Duration}ms",
                    entity.Id, entity.InputSchemaId, entity.OutputSchemaId, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetProcessorSchemaQueryResponse
                {
                    Success = true,
                    InputSchemaId = entity.InputSchemaId,
                    OutputSchemaId = entity.OutputSchemaId,
                    Message = "Processor schema information retrieved successfully"
                });
            }
            else
            {
                _logger.LogWarningWithCorrelation("Processor entity not found. ProcessorId: {ProcessorId}, Duration: {Duration}ms",
                    query.ProcessorId, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetProcessorSchemaQueryResponse
                {
                    Success = false,
                    InputSchemaId = null,
                    OutputSchemaId = null,
                    Message = $"Processor entity with ID {query.ProcessorId} not found"
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing GetProcessorSchemaQuery. ProcessorId: {ProcessorId}, Duration: {Duration}ms",
                query.ProcessorId, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new GetProcessorSchemaQueryResponse
            {
                Success = false,
                InputSchemaId = null,
                OutputSchemaId = null,
                Message = $"Error retrieving Processor schema information: {ex.Message}"
            });
        }
    }
}
