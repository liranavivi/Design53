using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Processor.File.Services;
using Shared.Correlation;
using Shared.Models;
using Shared.Processor.Application;
using Shared.Processor.Models;
using Shared.Processor.Services;
using System.Text.Json;

namespace Processor.File;

/// <summary>
/// Sample concrete implementation of BaseProcessorApplication
/// Demonstrates how to create a specific processor service
/// The base class now provides a complete default implementation that can be overridden if needed
/// </summary>
public class FileProcessorApplication : BaseProcessorApplication
{
    /// <summary>
    /// Override to add console logging for debugging
    /// </summary>
    protected override void ConfigureLogging(ILoggingBuilder logging)
    {
        // Add console logging for debugging - this will work alongside OpenTelemetry
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Debug);

        // Call base implementation
        base.ConfigureLogging(logging);
    }

    /// <summary>
    /// Override to add FileProcessor-specific services
    /// </summary>
    protected override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Call base implementation
        base.ConfigureServices(services, configuration);
        services.AddSingleton<IProcessorFileMetricsService, ProcessorFileMetricsService>();

    }

    /// <summary>
    /// Override to initialize file-specific metrics services
    /// </summary>
    protected override async Task InitializeCustomMetricsServicesAsync()
    {
        // Initialize file metrics service to ensure meters are registered with OpenTelemetry
        var fileMetricsService = ServiceProvider.GetRequiredService<IProcessorFileMetricsService>();
        
        // Call base implementation
        await base.InitializeCustomMetricsServicesAsync();
    }



    /// <summary>
    /// Concrete implementation of the activity processing logic
    /// This is where the specific processor business logic is implemented
    /// Handles input parsing and validation internally
    /// Returns a collection with a single item containing a new ExecutionId
    /// </summary>
    protected override async Task<IEnumerable<ProcessedActivityData>> ProcessActivityDataAsync(
        Guid processorId,
        Guid orchestratedFlowEntityId,
        Guid stepId,
        Guid executionId,
        List<AssignmentModel> entities,
        string inputData,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        // Get logger and metrics service from service provider
        var logger = ServiceProvider.GetRequiredService<ILogger<FileProcessorApplication>>();
        
        logger.LogInformationWithCorrelation(
            "Processing activity. ProcessorId: {ProcessorId}, StepId: {StepId}, ExecutionId: {ExecutionId}, EntitiesCount: {EntitiesCount}",
            processorId, stepId, executionId, entities.Count);

        // 1. Deserialize input data to local variables based on exampleData structure from ExecuteEntryPointsAsync
        string inputProcessorId = string.Empty;
        string inputOrchestratedFlowEntityId = string.Empty;
        int inputEntitiesProcessed = 0;
        DateTime inputProcessedAt = DateTime.UtcNow;
        string inputProcessingDuration = "0ms";
        bool inputDataReceived = false;
        bool inputMetadataReceived = false;
        string inputSampleData = "No input data";
        string[] inputEntityTypes = Array.Empty<string>();
        object[] inputEntities = Array.Empty<object>();

        try
        {
            if (string.IsNullOrEmpty(inputData))
            {
                logger.LogInformationWithCorrelation("No input data provided, using default values");
            }
            else
            {
                // Deserialize the input data based on the exampleData structure
                var inputObject = JsonSerializer.Deserialize<JsonElement>(inputData);

                // Extract top-level properties
                if (inputObject.TryGetProperty("processorId", out var processorIdElement))
                {
                    inputProcessorId = processorIdElement.GetString() ?? string.Empty;
                }

                if (inputObject.TryGetProperty("orchestratedFlowEntityId", out var orchestratedFlowElement))
                {
                    inputOrchestratedFlowEntityId = orchestratedFlowElement.GetString() ?? string.Empty;
                }

                if (inputObject.TryGetProperty("entitiesProcessed", out var entitiesProcessedElement))
                {
                    inputEntitiesProcessed = entitiesProcessedElement.GetInt32();
                }

                // Extract processingDetails properties
                if (inputObject.TryGetProperty("processingDetails", out var processingDetailsElement))
                {
                    if (processingDetailsElement.TryGetProperty("processedAt", out var processedAtElement))
                    {
                        if (DateTime.TryParse(processedAtElement.GetString(), out var parsedDate))
                        {
                            inputProcessedAt = parsedDate;
                        }
                    }

                    if (processingDetailsElement.TryGetProperty("processingDuration", out var durationElement))
                    {
                        inputProcessingDuration = durationElement.GetString() ?? "0ms";
                    }

                    if (processingDetailsElement.TryGetProperty("inputDataReceived", out var dataReceivedElement))
                    {
                        inputDataReceived = dataReceivedElement.GetBoolean();
                    }

                    if (processingDetailsElement.TryGetProperty("inputMetadataReceived", out var metadataReceivedElement))
                    {
                        inputMetadataReceived = metadataReceivedElement.GetBoolean();
                    }

                    if (processingDetailsElement.TryGetProperty("sampleData", out var sampleDataElement))
                    {
                        inputSampleData = sampleDataElement.GetString() ?? "No sample data";
                    }

                    if (processingDetailsElement.TryGetProperty("entityTypes", out var entityTypesElement))
                    {
                        var entityTypesList = new List<string>();
                        foreach (var entityType in entityTypesElement.EnumerateArray())
                        {
                            var typeString = entityType.GetString();
                            if (!string.IsNullOrEmpty(typeString))
                            {
                                entityTypesList.Add(typeString);
                            }
                        }
                        inputEntityTypes = entityTypesList.ToArray();
                    }

                    if (processingDetailsElement.TryGetProperty("entities", out var entitiesElement))
                    {
                        var entitiesList = new List<object>();
                        foreach (var entity in entitiesElement.EnumerateArray())
                        {
                            var entityObj = new
                            {
                                entityId = entity.TryGetProperty("entityId", out var entityIdElement) ? entityIdElement.GetString() : string.Empty,
                                type = entity.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : string.Empty,
                                assignmentType = entity.TryGetProperty("assignmentType", out var assignmentTypeElement) ? assignmentTypeElement.GetString() : string.Empty
                            };
                            entitiesList.Add(entityObj);
                        }
                        inputEntities = entitiesList.ToArray();
                    }
                }

                // Get configuration and file metrics service from service provider
                var config = ServiceProvider.GetRequiredService<IOptions<ProcessorConfiguration>>().Value;
                var fileMetricsService = ServiceProvider.GetService<IProcessorFileMetricsService>();

                // Convert string to Guid for metrics service
                Guid? orchestratedFlowGuid = null;
                if (!string.IsNullOrEmpty(inputOrchestratedFlowEntityId) && Guid.TryParse(inputOrchestratedFlowEntityId, out var parsedGuid))
                {
                    orchestratedFlowGuid = parsedGuid;
                }

                fileMetricsService?.RecordDeserializedInputData(config.Name, config.Version, orchestratedFlowGuid);
                
                logger.LogInformationWithCorrelation("Successfully deserialized input data. ProcessorId: {ProcessorId}, EntitiesProcessed: {EntitiesProcessed}, SampleData: {SampleData}",
                    inputProcessorId, inputEntitiesProcessed, inputSampleData);
            }
        }
        catch (JsonException ex)
        {
            logger.LogErrorWithCorrelation(ex, "Failed to deserialize input data: {ErrorMessage}", ex.Message);

            // Record validation exception
            var healthMetricsService = ServiceProvider.GetService<IProcessorHealthMetricsService>();
            healthMetricsService?.RecordException("JsonException", "error", isCritical: false);

            throw new InvalidOperationException($"Failed to deserialize input data: {ex.Message}");
        }



        // Simulate some processing time and track it for metrics
        var processingStart = DateTime.UtcNow;
        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        var processingDuration = DateTime.UtcNow - processingStart;

        // Log input data details using deserialized local variables
        logger.LogInformationWithCorrelation(
            "Input data details - ProcessorId: {InputProcessorId}, OrchestratedFlowEntityId: {InputOrchestratedFlowEntityId}, EntitiesProcessed: {InputEntitiesProcessed}, ProcessedAt: {InputProcessedAt}, Duration: {InputProcessingDuration}",
            inputProcessorId, inputOrchestratedFlowEntityId, inputEntitiesProcessed, inputProcessedAt, inputProcessingDuration);

        logger.LogInformationWithCorrelation(
            "Input metadata - DataReceived: {InputDataReceived}, MetadataReceived: {InputMetadataReceived}, SampleData: {InputSampleData}, EntityTypes: {InputEntityTypes}",
            inputDataReceived, inputMetadataReceived, inputSampleData, string.Join(", ", inputEntityTypes));

        // Extract all validatable entities using the interface
        var validatableEntities = entities
            .OfType<IValidatableAssignmentModel>()
            .ToList();

        foreach (var assignmentEntity in validatableEntities)
        {
            inputSampleData += $"The Step {stepId}  -----   "+ assignmentEntity.GetValidatableModel().Payload + Environment.NewLine;
        }

        // Create ProcessedActivityData.Data example for testing schemas between orchestrator and processor
        var processedActivityData = new
        {
            processorId = processorId.ToString(),
            orchestratedFlowEntityId = orchestratedFlowEntityId.ToString(),
            entitiesProcessed = entities.Count,
            processingDetails = new
            {
                processedAt = DateTime.UtcNow,
                processingDuration = "0ms",
                inputDataReceived = true,
                inputMetadataReceived = true,
                sampleData = inputSampleData ,
                entityTypes = entities.Select(e => e.GetType().Name).Distinct().ToArray(),
                entities = entities.Select(e => new
                {
                    entityId = e.EntityId.ToString(),
                    type = e.GetType().Name,
                    assignmentType = e.GetType().Name
                }).ToArray()
            }
        };

        // Return processed data incorporating deserialized local variables
        // Return collection with single item containing new ExecutionId as specified
        var singleResult = new ProcessedActivityData
        {
            Result = "File processing completed successfully",
            Status = ActivityExecutionStatus.Completed,
            Data = processedActivityData,
            ProcessorName = "EnhancedFileProcessor",
            Version = "3.0",
            ExecutionId = Guid.NewGuid() // Always generate new ExecutionId as specified
        };

        return new[] { singleResult };
    }
}