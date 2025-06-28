using Shared.Correlation;

namespace Manager.Assignment.Services;

/// <summary>
/// Service for validating referential integrity with OrchestratedFlow entities
/// </summary>
public class OrchestratedFlowValidationService : IOrchestratedFlowValidationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrchestratedFlowValidationService> _logger;

    public OrchestratedFlowValidationService(
        HttpClient httpClient,
        ILogger<OrchestratedFlowValidationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> CheckAssignmentReferencesAsync(Guid assignmentId)
    {
        _logger.LogInformationWithCorrelation("Starting assignment reference validation. AssignmentId: {AssignmentId}", assignmentId);

        try
        {
            var response = await _httpClient.GetAsync($"api/orchestratedflow/assignment/{assignmentId}/exists");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var hasReferences = bool.Parse(content);

                _logger.LogInformationWithCorrelation("Successfully validated assignment references. AssignmentId: {AssignmentId}, HasReferences: {HasReferences}",
                    assignmentId, hasReferences);

                return hasReferences;
            }

            // Fail-safe: if we can't validate, assume there are references
            _logger.LogWarningWithCorrelation("Failed to validate assignment references - service returned error. AssignmentId: {AssignmentId}, StatusCode: {StatusCode}",
                assignmentId, response.StatusCode);
            return true;
        }
        catch (HttpRequestException ex)
        {
            // Fail-safe: if service is unavailable, assume there are references
            _logger.LogErrorWithCorrelation(ex, "HTTP error validating assignment references - service may be unavailable. AssignmentId: {AssignmentId}",
                assignmentId);
            return true;
        }
        catch (TaskCanceledException ex)
        {
            // Fail-safe: if request times out, assume there are references
            _logger.LogErrorWithCorrelation(ex, "Timeout validating assignment references. AssignmentId: {AssignmentId}", assignmentId);
            return true;
        }
        catch (Exception ex)
        {
            // Fail-safe: if any other error occurs, assume there are references
            _logger.LogErrorWithCorrelation(ex, "Unexpected error validating assignment references. AssignmentId: {AssignmentId}", assignmentId);
            return true;
        }
    }
}
