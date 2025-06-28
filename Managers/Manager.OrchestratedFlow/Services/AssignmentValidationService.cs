using Shared.Correlation;

namespace Manager.OrchestratedFlow.Services;

/// <summary>
/// Service for validating references to Assignment entities
/// </summary>
public class AssignmentValidationService : IAssignmentValidationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AssignmentValidationService> _logger;

    public AssignmentValidationService(
        HttpClient httpClient,
        ILogger<AssignmentValidationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> ValidateAssignmentExistsAsync(Guid assignmentId)
    {
        _logger.LogInformationWithCorrelation("Starting assignment existence validation. AssignmentId: {AssignmentId}", assignmentId);

        try
        {
            var response = await _httpClient.GetAsync($"api/assignment/{assignmentId}");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformationWithCorrelation("Successfully validated assignment exists. AssignmentId: {AssignmentId}", assignmentId);
                return true;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarningWithCorrelation("Assignment not found. AssignmentId: {AssignmentId}", assignmentId);
                return false;
            }

            // Fail-safe: if we can't validate, assume assignment doesn't exist
            _logger.LogWarningWithCorrelation("Failed to validate assignment existence - service returned error. AssignmentId: {AssignmentId}, StatusCode: {StatusCode}",
                assignmentId, response.StatusCode);
            return false;
        }
        catch (HttpRequestException ex)
        {
            // Fail-safe: if service is unavailable, assume assignment doesn't exist
            _logger.LogErrorWithCorrelation(ex, "HTTP error validating assignment existence - service may be unavailable. AssignmentId: {AssignmentId}",
                assignmentId);
            return false;
        }
        catch (TaskCanceledException ex)
        {
            // Fail-safe: if request times out, assume assignment doesn't exist
            _logger.LogErrorWithCorrelation(ex, "Timeout validating assignment existence. AssignmentId: {AssignmentId}", assignmentId);
            return false;
        }
        catch (Exception ex)
        {
            // Fail-safe: if any other error occurs, assume assignment doesn't exist
            _logger.LogErrorWithCorrelation(ex, "Unexpected error validating assignment existence. AssignmentId: {AssignmentId}", assignmentId);
            return false;
        }
    }

    public async Task<bool> ValidateAssignmentsExistAsync(IEnumerable<Guid> assignmentIds)
    {
        if (assignmentIds == null || !assignmentIds.Any())
        {
            _logger.LogInformationWithCorrelation("No assignment IDs provided for validation - returning true");
            return true;
        }

        var assignmentIdsList = assignmentIds.ToList();
        _logger.LogInformationWithCorrelation("Starting batch assignment existence validation. AssignmentIds: {AssignmentIds}", 
            string.Join(",", assignmentIdsList));

        // Validate all assignments in parallel for performance
        var validationTasks = assignmentIdsList.Select(ValidateAssignmentExistsAsync);
        var results = await Task.WhenAll(validationTasks);

        var allExist = results.All(exists => exists);
        
        _logger.LogInformationWithCorrelation("Completed batch assignment existence validation. AssignmentIds: {AssignmentIds}, AllExist: {AllExist}", 
            string.Join(",", assignmentIdsList), allExist);

        return allExist;
    }
}
