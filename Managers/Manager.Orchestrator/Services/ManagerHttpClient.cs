using System.Text.Json;
using Polly;
using Shared.Correlation;
using Shared.Entities;
using Shared.Models;

namespace Manager.Orchestrator.Services;

/// <summary>
/// HTTP client for communication with other entity managers with resilience patterns
/// </summary>
public class ManagerHttpClient : IManagerHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ManagerHttpClient> _logger;
    private readonly IConfiguration _configuration;
    private readonly IAsyncPolicy<HttpResponseMessage> _resilientPolicy;
    private readonly JsonSerializerOptions _jsonOptions;

    public ManagerHttpClient(
        HttpClient httpClient,
        ILogger<ManagerHttpClient> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;

        // Configure JSON options
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        // Configure resilience policy
        _resilientPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: _configuration.GetValue<int>("HttpClient:MaxRetries", 3),
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(
                    _configuration.GetValue<int>("HttpClient:RetryDelayMs", 1000) * Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarningWithCorrelation("HTTP request retry {RetryCount} after {Delay}ms. Reason: {Reason}",
                        retryCount, timespan.TotalMilliseconds, outcome.Exception?.Message ?? outcome.Result?.ReasonPhrase);
                });
    }

    public async Task<OrchestratedFlowEntity?> GetOrchestratedFlowAsync(Guid orchestratedFlowId)
    {
        var baseUrl = _configuration["ManagerUrls:OrchestratedFlow"];
        var url = $"{baseUrl}/api/OrchestratedFlow/{orchestratedFlowId}";

        _logger.LogInformationWithCorrelation("Retrieving orchestrated flow. OrchestratedFlowId: {OrchestratedFlowId}, Url: {Url}",
            orchestratedFlowId, url);

        try
        {
            var response = await _resilientPolicy.ExecuteAsync(async () =>
            {
                return await _httpClient.GetAsync(url);
            });

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var entity = JsonSerializer.Deserialize<OrchestratedFlowEntity>(content, _jsonOptions);

                _logger.LogInformationWithCorrelation("Successfully retrieved orchestrated flow. OrchestratedFlowId: {OrchestratedFlowId}, WorkflowId: {WorkflowId}, AssignmentCount: {AssignmentCount}",
                    orchestratedFlowId, entity?.WorkflowId, entity?.AssignmentIds?.Count ?? 0);

                return entity;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarningWithCorrelation("Orchestrated flow not found. OrchestratedFlowId: {OrchestratedFlowId}",
                    orchestratedFlowId);
                return null;
            }

            _logger.LogErrorWithCorrelation("Failed to retrieve orchestrated flow. OrchestratedFlowId: {OrchestratedFlowId}, StatusCode: {StatusCode}, Reason: {Reason}",
                orchestratedFlowId, response.StatusCode, response.ReasonPhrase);

            throw new HttpRequestException($"Failed to retrieve orchestrated flow: {response.StatusCode} - {response.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving orchestrated flow. OrchestratedFlowId: {OrchestratedFlowId}",
                orchestratedFlowId);
            throw;
        }
    }

    public async Task<StepManagerModel> GetStepManagerDataAsync(Guid workflowId)
    {
        _logger.LogInformationWithCorrelation("Retrieving step manager data. WorkflowId: {WorkflowId}",
            workflowId);

        try
        {
            // Step 1: Get workflow from Workflow Manager to get step IDs
            var workflowBaseUrl = _configuration["ManagerUrls:Workflow"];
            var workflowUrl = $"{workflowBaseUrl}/api/Workflow/{workflowId}";

            _logger.LogInformationWithCorrelation("Retrieving workflow to get step IDs. WorkflowId: {WorkflowId}, BaseUrl: {BaseUrl}, FullUrl: {Url}",
                workflowId, workflowBaseUrl, workflowUrl);

            var workflowResponse = await _resilientPolicy.ExecuteAsync(async () =>
            {
                return await _httpClient.GetAsync(workflowUrl);
            });

            if (!workflowResponse.IsSuccessStatusCode)
            {
                _logger.LogErrorWithCorrelation("Failed to retrieve workflow. WorkflowId: {WorkflowId}, StatusCode: {StatusCode}, Reason: {Reason}",
                    workflowId, workflowResponse.StatusCode, workflowResponse.ReasonPhrase);
                throw new HttpRequestException($"Failed to retrieve workflow: {workflowResponse.StatusCode} - {workflowResponse.ReasonPhrase}");
            }

            var workflowContent = await workflowResponse.Content.ReadAsStringAsync();
            var workflow = JsonSerializer.Deserialize<WorkflowEntity>(workflowContent, _jsonOptions);

            if (workflow == null || !workflow.StepIds.Any())
            {
                _logger.LogWarningWithCorrelation("Workflow not found or has no steps. WorkflowId: {WorkflowId}",
                    workflowId);
                return new StepManagerModel();
            }

            _logger.LogDebugWithCorrelation("Retrieved workflow with {StepCount} steps. WorkflowId: {WorkflowId}, StepIds: {StepIds}",
                workflow.StepIds.Count, workflowId, string.Join(",", workflow.StepIds));

            // Step 2: Get individual steps from Step Manager
            var stepBaseUrl = _configuration["ManagerUrls:Step"];
            var stepTasks = workflow.StepIds.Select(async stepId =>
            {
                var stepUrl = $"{stepBaseUrl}/api/Step/{stepId}";

                var stepResponse = await _resilientPolicy.ExecuteAsync(async () =>
                {
                    return await _httpClient.GetAsync(stepUrl);
                });

                if (stepResponse.IsSuccessStatusCode)
                {
                    var stepContent = await stepResponse.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<StepEntity>(stepContent, _jsonOptions);
                }

                _logger.LogWarningWithCorrelation("Failed to retrieve step. StepId: {StepId}, StatusCode: {StatusCode}",
                    stepId, stepResponse.StatusCode);
                return null;
            });

            var stepResults = await Task.WhenAll(stepTasks);
            var steps = stepResults.Where(s => s != null).Cast<StepEntity>().ToList();

            var model = new StepManagerModel
            {
                ProcessorIds = steps.Select(s => s.ProcessorId).Distinct().ToList(),
                StepIds = steps.Select(s => s.Id).ToList(),
                NextStepIds = steps.SelectMany(s => s.NextStepIds).Distinct().ToList(),
                StepEntities = steps.ToDictionary(s => s.Id, s => s)
            };

            _logger.LogInformationWithCorrelation("Successfully retrieved step manager data. WorkflowId: {WorkflowId}, StepCount: {StepCount}, ProcessorCount: {ProcessorCount}, NextStepCount: {NextStepCount}",
                workflowId, model.StepIds.Count, model.ProcessorIds.Count, model.NextStepIds.Count);

            return model;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving step manager data. WorkflowId: {WorkflowId}",
                workflowId);
            throw;
        }
    }

    public async Task<AssignmentManagerModel> GetAssignmentManagerDataAsync(List<Guid> assignmentIds)
    {
        var model = new AssignmentManagerModel();

        if (!assignmentIds.Any())
        {
            _logger.LogInformationWithCorrelation("No assignment IDs provided, returning empty assignment manager model");
            return model;
        }

        var baseUrl = _configuration["ManagerUrls:Assignment"];

        _logger.LogInformationWithCorrelation("Retrieving assignment manager data. AssignmentIds: {AssignmentIds}",
            string.Join(",", assignmentIds));

        try
        {
            // Get all assignments in parallel
            var assignmentTasks = assignmentIds.Select(async assignmentId =>
            {
                var url = $"{baseUrl}/api/Assignment/{assignmentId}";

                var response = await _resilientPolicy.ExecuteAsync(async () =>
                {
                    return await _httpClient.GetAsync(url);
                });

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<AssignmentEntity>(content, _jsonOptions);
                }

                _logger.LogWarningWithCorrelation("Failed to retrieve assignment. AssignmentId: {AssignmentId}, StatusCode: {StatusCode}",
                    assignmentId, response.StatusCode);
                return null;
            });

            var assignments = await Task.WhenAll(assignmentTasks);
            var validAssignments = assignments.Where(a => a != null).Cast<AssignmentEntity>().ToList();

            // Group assignments by step ID and process entities
            foreach (var assignment in validAssignments)
            {
                if (!model.Assignments.ContainsKey(assignment.StepId))
                {
                    model.Assignments[assignment.StepId] = new List<AssignmentModel>();
                }

                // Process each entity ID in the assignment
                foreach (var entityId in assignment.EntityIds)
                {
                    var assignmentModel = await ProcessAssignmentEntityAsync(entityId);
                    if (assignmentModel != null)
                    {
                        model.Assignments[assignment.StepId].Add(assignmentModel);
                    }
                }
            }

            var totalAssignments = model.Assignments.Values.Sum(list => list.Count);
            _logger.LogInformationWithCorrelation("Successfully retrieved assignment manager data. AssignmentCount: {AssignmentCount}, TotalEntities: {TotalEntities}",
                validAssignments.Count, totalAssignments);

            return model;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving assignment manager data. AssignmentIds: {AssignmentIds}",
                string.Join(",", assignmentIds));
            throw;
        }
    }

    private async Task<AssignmentModel?> ProcessAssignmentEntityAsync(Guid entityId)
    {
        try
        {
            // Try to get as address entity first
            var addressModel = await TryGetAddressEntityAsync(entityId);
            if (addressModel != null)
            {
                return new AddressAssignmentModel { EntityId = entityId, Address = addressModel };
            }

            // Try to get as delivery entity
            var deliveryModel = await TryGetDeliveryEntityAsync(entityId);
            if (deliveryModel != null)
            {
                return new DeliveryAssignmentModel { EntityId = entityId, Delivery = deliveryModel };
            }

            _logger.LogWarningWithCorrelation("Entity not found in Address or Delivery managers. EntityId: {EntityId}", entityId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error processing assignment entity. EntityId: {EntityId}", entityId);
            return null;
        }
    }

    private async Task<AddressModel?> TryGetAddressEntityAsync(Guid entityId)
    {
        try
        {
            var baseUrl = _configuration["ManagerUrls:Address"];
            var url = $"{baseUrl}/api/Address/{entityId}";

            var response = await _resilientPolicy.ExecuteAsync(async () =>
            {
                return await _httpClient.GetAsync(url);
            });

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var addressEntity = JsonSerializer.Deserialize<AddressEntity>(content, _jsonOptions);

                if (addressEntity != null)
                {
                    var schemaDefinition = await GetSchemaDefinitionAsync(addressEntity.SchemaId);

                    return new AddressModel
                    {
                        Name = addressEntity.Name,
                        Version = addressEntity.Version,
                        ConnectionString = addressEntity.ConnectionString,
                        Payload = addressEntity.Payload, 
                        SchemaDefinition = schemaDefinition
                    };
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Failed to retrieve entity as address. EntityId: {EntityId}", entityId);
            return null;
        }
    }

    private async Task<DeliveryModel?> TryGetDeliveryEntityAsync(Guid entityId)
    {
        try
        {
            var baseUrl = _configuration["ManagerUrls:Delivery"];
            var url = $"{baseUrl}/api/Delivery/{entityId}";

            var response = await _resilientPolicy.ExecuteAsync(async () =>
            {
                return await _httpClient.GetAsync(url);
            });

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var deliveryEntity = JsonSerializer.Deserialize<DeliveryEntity>(content, _jsonOptions);

                if (deliveryEntity != null)
                {
                    var schemaDefinition = await GetSchemaDefinitionAsync(deliveryEntity.SchemaId);

                    return new DeliveryModel
                    {
                        Name = deliveryEntity.Name,
                        Version = deliveryEntity.Version,
                        Payload = deliveryEntity.Payload,
                        SchemaDefinition = schemaDefinition
                    };
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Failed to retrieve entity as delivery. EntityId: {EntityId}", entityId);
            return null;
        }
    }

    public async Task<string> GetSchemaDefinitionAsync(Guid schemaId)
    {
        var baseUrl = _configuration["ManagerUrls:Schema"];
        var url = $"{baseUrl}/api/Schema/{schemaId}";

        _logger.LogDebugWithCorrelation("Retrieving schema definition. SchemaId: {SchemaId}, Url: {Url}",
            schemaId, url);

        try
        {
            var response = await _resilientPolicy.ExecuteAsync(async () =>
            {
                return await _httpClient.GetAsync(url);
            });

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var schemaEntity = JsonSerializer.Deserialize<SchemaEntity>(content, _jsonOptions);

                _logger.LogDebugWithCorrelation("Successfully retrieved schema definition. SchemaId: {SchemaId}",
                    schemaId);

                return schemaEntity?.Definition ?? string.Empty;
            }

            _logger.LogWarningWithCorrelation("Failed to retrieve schema definition. SchemaId: {SchemaId}, StatusCode: {StatusCode}",
                schemaId, response.StatusCode);

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving schema definition. SchemaId: {SchemaId}",
                schemaId);
            return string.Empty;
        }
    }
}
