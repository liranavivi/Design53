using System.Diagnostics;
using Manager.Assignment.Repositories;
using MassTransit;
using Shared.MassTransit.Commands;
using Shared.Correlation;

namespace Manager.Assignment.Consumers;

public class GetAssignmentDetailsQueryConsumer : IConsumer<GetAssignmentDetailsQuery>
{
    private readonly IAssignmentEntityRepository _repository;
    private readonly ILogger<GetAssignmentDetailsQueryConsumer> _logger;

    public GetAssignmentDetailsQueryConsumer(
        IAssignmentEntityRepository repository,
        ILogger<GetAssignmentDetailsQueryConsumer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetAssignmentDetailsQuery> context)
    {
        var query = context.Message;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformationWithCorrelation("Processing GetAssignmentDetailsQuery. AssignmentId: {AssignmentId}, RequestedBy: {RequestedBy}",
            query.AssignmentId, query.RequestedBy);

        try
        {
            var entity = await _repository.GetByIdAsync(query.AssignmentId);

            stopwatch.Stop();

            if (entity != null)
            {
                _logger.LogInformationWithCorrelation("Successfully processed GetAssignmentDetailsQuery. Found Assignment Id: {Id}, StepId: {StepId}, EntityIds count: {EntityIdsCount}, Duration: {Duration}ms",
                    entity.Id, entity.StepId, entity.EntityIds?.Count ?? 0, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetAssignmentDetailsQueryResponse
                {
                    Success = true,
                    StepId = entity.StepId,
                    EntityIds = entity.EntityIds ?? new List<Guid>(),
                    Message = "Assignment details retrieved successfully"
                });
            }
            else
            {
                _logger.LogWarningWithCorrelation("Assignment entity not found. AssignmentId: {AssignmentId}, Duration: {Duration}ms",
                    query.AssignmentId, stopwatch.ElapsedMilliseconds);

                await context.RespondAsync(new GetAssignmentDetailsQueryResponse
                {
                    Success = false,
                    StepId = Guid.Empty,
                    EntityIds = new List<Guid>(),
                    Message = $"Assignment entity with ID {query.AssignmentId} not found"
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorWithCorrelation(ex, "Error processing GetAssignmentDetailsQuery. AssignmentId: {AssignmentId}, Duration: {Duration}ms",
                query.AssignmentId, stopwatch.ElapsedMilliseconds);

            await context.RespondAsync(new GetAssignmentDetailsQueryResponse
            {
                Success = false,
                StepId = Guid.Empty,
                EntityIds = new List<Guid>(),
                Message = $"Error retrieving Assignment details: {ex.Message}"
            });
        }
    }
}
