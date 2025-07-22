using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Correlation;
using Shared.Entities;
using Shared.Extensions;
using Shared.MassTransit.Commands;
using Shared.Models;
using Shared.Processor.Models;
using Shared.Services;
using System.Diagnostics;
using System.Text.Json;

namespace Shared.Processor.Services;

/// <summary>
/// Core service for managing processor functionality and activity processing
/// </summary>
public class ProcessorService : IProcessorService
{
    private readonly IActivityExecutor _activityExecutor;
    private readonly ICacheService _cacheService;
    private readonly ISchemaValidator _schemaValidator;
    private readonly IBus _bus;
    private readonly ProcessorConfiguration _config;
    private readonly SchemaValidationConfiguration _validationConfig;
    private readonly ProcessorInitializationConfiguration? _initializationConfig;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProcessorService> _logger;
    private readonly IPerformanceMetricsService? _performanceMetricsService;
    private readonly IProcessorHealthMetricsService? _healthMetricsService;
    private readonly ActivitySource _activitySource;
    private readonly DateTime _startTime;

    private Guid? _processorId;
    private readonly object _processorIdLock = new();

    // Schema health tracking - Initialize as unhealthy until validation completes
    private bool _inputSchemaHealthy = false;
    private bool _outputSchemaHealthy = false;
    private bool _schemaIdsValid = false;
    private string _inputSchemaErrorMessage = "Schema not yet validated";
    private string _outputSchemaErrorMessage = "Schema not yet validated";
    private string _schemaValidationErrorMessage = "Schema validation not yet performed";
    private readonly object _schemaHealthLock = new();

    // Implementation hash validation tracking - Initialize as unhealthy until validation completes
    private bool _implementationHashValid = false;
    private string _implementationHashErrorMessage = "Implementation hash not yet validated";

    // Initialization status tracking
    private bool _isInitialized = false;
    private bool _isInitializing = false;
    private string _initializationErrorMessage = string.Empty;
    private readonly object _initializationLock = new();

    
    public ProcessorService(
        IActivityExecutor activityExecutor,
        ICacheService cacheService,
        ISchemaValidator schemaValidator,
        IBus bus,
        IOptions<ProcessorConfiguration> config,
        IOptions<SchemaValidationConfiguration> validationConfig,
        IConfiguration configuration,
        ILogger<ProcessorService> logger,
        IProcessorMetricsLabelService labelService,
        IPerformanceMetricsService? performanceMetricsService = null,
        IProcessorHealthMetricsService? healthMetricsService = null,
        IOptions<ProcessorInitializationConfiguration>? initializationConfig = null)
    {
        _activityExecutor = activityExecutor;
        _cacheService = cacheService;
        _schemaValidator = schemaValidator;
        _bus = bus;
        _config = config.Value;
        _validationConfig = validationConfig.Value;
        _initializationConfig = initializationConfig?.Value;
        _configuration = configuration;
        _logger = logger;
        _performanceMetricsService = performanceMetricsService;
        _healthMetricsService = healthMetricsService;
        _activitySource = new ActivitySource(ActivitySources.Services);
        _startTime = DateTime.UtcNow;
    }

    public async Task InitializeAsync()
    {
        await InitializeAsync(CancellationToken.None);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        // Generate and set correlation ID for processor initialization
        var correlationId = Guid.NewGuid();
        CorrelationIdContext.SetCorrelationIdStatic(correlationId);

        using var activity = _activitySource.StartActivity("InitializeProcessor");
        activity?.SetTag("correlation.id", correlationId.ToString());

        // Set initialization status
        lock (_initializationLock)
        {
            _isInitializing = true;
            _isInitialized = false;
            _initializationErrorMessage = string.Empty;
        }

        _logger.LogInformationWithCorrelation(
            "Initializing processor - {ProcessorName} v{ProcessorVersion}",
            _config.Name, _config.Version);

        try
        {
            // Get initialization configuration
            var initConfig = _initializationConfig ?? new ProcessorInitializationConfiguration();

            if (!initConfig.RetryEndlessly)
            {
                // Legacy behavior: retry limited times then throw
                await InitializeWithLimitedRetriesAsync(activity, cancellationToken);
            }
            else
            {
                // New behavior: retry endlessly until successful
                await InitializeWithEndlessRetriesAsync(activity, cancellationToken);
            }

            // Mark as successfully initialized
            lock (_initializationLock)
            {
                _isInitialized = true;
                _isInitializing = false;
                _initializationErrorMessage = string.Empty;
            }

            _logger.LogInformationWithCorrelation(
                "Processor initialization completed successfully - {ProcessorName} v{ProcessorVersion}",
                _config.Name, _config.Version);
        }
        catch (Exception ex)
        {
            // Mark initialization as failed
            lock (_initializationLock)
            {
                _isInitialized = false;
                _isInitializing = false;
                _initializationErrorMessage = ex.Message;
            }

            // Record initialization exception
            _healthMetricsService?.RecordException(ex.GetType().Name, "critical", isCritical: true);

            _logger.LogErrorWithCorrelation(ex,
                "Processor initialization failed - {ProcessorName} v{ProcessorVersion}",
                _config.Name, _config.Version);

            throw;
        }
    }

    private async Task InitializeWithEndlessRetriesAsync(Activity? activity, CancellationToken cancellationToken)
    {
        var initConfig = _initializationConfig ?? new ProcessorInitializationConfiguration();
        var attempt = 0;
        var currentDelay = initConfig.RetryDelay;

        _logger.LogInformationWithCorrelation(
            "Starting endless initialization retry loop for processor {CompositeKey}. RetryDelay: {RetryDelay}, MaxRetryDelay: {MaxRetryDelay}, UseExponentialBackoff: {UseExponentialBackoff}",
            _config.GetCompositeKey(), initConfig.RetryDelay, initConfig.MaxRetryDelay, initConfig.UseExponentialBackoff);

        // Always retrieve schema definitions first to validate configuration
        _logger.LogInformationWithCorrelation("Retrieving schema definitions to validate configuration before processor operations");
        await RetrieveSchemaDefinitionsAsync();

        while (!cancellationToken.IsCancellationRequested)
        {
            attempt++;

            try
            {
                if (initConfig.LogRetryAttempts)
                {
                    _logger.LogDebugWithCorrelation("Requesting processor by composite key: {CompositeKey} (attempt {Attempt})",
                        _config.GetCompositeKey(), attempt);
                }

                // Always get processor query to check if processor exists
                var getQuery = new GetProcessorQuery
                {
                    CompositeKey = _config.GetCompositeKey()
                };

                using var timeoutCts = new CancellationTokenSource(initConfig.InitializationTimeout);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                var response = await _bus.Request<GetProcessorQuery, GetProcessorQueryResponse>(
                    getQuery, combinedCts.Token, initConfig.InitializationTimeout);

                if (response.Message.Success && response.Message.Entity != null)
                {
                    // Existing processor found
                    var processorEntity = response.Message.Entity;

                    lock (_processorIdLock)
                    {
                        _processorId = processorEntity.Id;
                    }

                    _logger.LogInformationWithCorrelation(
                        "Found existing processor after {Attempts} attempts. ProcessorId: {ProcessorId}, CompositeKey: {CompositeKey}",
                        attempt, _processorId, _config.GetCompositeKey());

                    activity?.SetProcessorTags(_processorId.Value, _config.Name, _config.Version);

                    // For existing processor: validate schema IDs and implementation hash
                    ValidateSchemaIds(processorEntity);
                    ValidateImplementationHash(processorEntity);

                    // Success - exit retry loop
                    _logger.LogInformationWithCorrelation(
                        "Processor initialization completed successfully after {Attempts} attempts. ProcessorId: {ProcessorId}",
                        attempt, _processorId);
                    return;
                }
                else
                {
                    // Processor not found - create new processor
                    if (initConfig.LogRetryAttempts)
                    {
                        _logger.LogInformationWithCorrelation(
                            "Processor not found, creating new processor. CompositeKey: {CompositeKey} (attempt {Attempt})",
                            _config.GetCompositeKey(), attempt);
                    }

                    await CreateProcessorAsync();

                    _logger.LogInformationWithCorrelation(
                        "Processor creation and initialization completed successfully after {Attempts} attempts. ProcessorId: {ProcessorId}",
                        attempt, _processorId);
                    return; // Success - exit retry loop
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformationWithCorrelation(
                    "Processor initialization cancelled after {Attempts} attempts. CompositeKey: {CompositeKey}",
                    attempt, _config.GetCompositeKey());
                throw;
            }
            catch (RequestTimeoutException ex)
            {
                // Record timeout exception metrics
                _healthMetricsService?.RecordException(ex.GetType().Name, "warning", isCritical: false);

                if (initConfig.LogRetryAttempts)
                {
                    _logger.LogWarningWithCorrelation(ex,
                        "Timeout while requesting processor (attempt {Attempt}). CompositeKey: {CompositeKey}. Retrying in {DelaySeconds} seconds...",
                        attempt, _config.GetCompositeKey(), currentDelay.TotalSeconds);
                }

                // Wait before next retry
                await Task.Delay(currentDelay, cancellationToken);

                // Calculate next delay with exponential backoff if enabled
                if (initConfig.UseExponentialBackoff)
                {
                    currentDelay = TimeSpan.FromMilliseconds(Math.Min(
                        currentDelay.TotalMilliseconds * 2,
                        initConfig.MaxRetryDelay.TotalMilliseconds));
                }
            }
            catch (Exception ex)
            {
                activity?.SetErrorTags(ex);

                // Record exception metrics for initialization failures
                _healthMetricsService?.RecordException(ex.GetType().Name, "error", isCritical: true);

                _logger.LogErrorWithCorrelation(ex,
                    "Unexpected error during processor initialization (attempt {Attempt}). CompositeKey: {CompositeKey}. Retrying in {DelaySeconds} seconds...",
                    attempt, _config.GetCompositeKey(), currentDelay.TotalSeconds);

                // Wait before next retry
                await Task.Delay(currentDelay, cancellationToken);

                // Calculate next delay with exponential backoff if enabled
                if (initConfig.UseExponentialBackoff)
                {
                    currentDelay = TimeSpan.FromMilliseconds(Math.Min(
                        currentDelay.TotalMilliseconds * 2,
                        initConfig.MaxRetryDelay.TotalMilliseconds));
                }
            }
        }
    }

    private async Task InitializeWithLimitedRetriesAsync(Activity? activity, CancellationToken cancellationToken)
    {
        var initConfig = _initializationConfig ?? new ProcessorInitializationConfiguration();

        // Legacy behavior: retry limited times then throw
        const int maxRetries = 3;
        var baseDelay = TimeSpan.FromSeconds(2);
        var maxDelay = TimeSpan.FromSeconds(10);

        RequestTimeoutException? lastTimeoutException = null;

        // Always retrieve schema definitions first to validate configuration
        _logger.LogInformationWithCorrelation("Retrieving schema definitions to validate configuration before processor operations");
        await RetrieveSchemaDefinitionsAsync();

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogDebugWithCorrelation("Requesting processor by composite key: {CompositeKey} (attempt {Attempt}/{MaxRetries})",
                    _config.GetCompositeKey(), attempt, maxRetries);

                // Always get processor query to check if processor exists
                var getQuery = new GetProcessorQuery
                {
                    CompositeKey = _config.GetCompositeKey()
                };

                var response = await _bus.Request<GetProcessorQuery, GetProcessorQueryResponse>(
                    getQuery, cancellationToken, initConfig.InitializationTimeout);

                if (response.Message.Success && response.Message.Entity != null)
                {
                    // Existing processor found
                    var processorEntity = response.Message.Entity;

                    lock (_processorIdLock)
                    {
                        _processorId = processorEntity.Id;
                    }

                    _logger.LogInformationWithCorrelation(
                        "Found existing processor. ProcessorId: {ProcessorId}, CompositeKey: {CompositeKey}",
                        _processorId, _config.GetCompositeKey());

                    activity?.SetProcessorTags(_processorId.Value, _config.Name, _config.Version);

                    // For existing processor: validate schema IDs and implementation hash
                    ValidateSchemaIds(processorEntity);
                    ValidateImplementationHash(processorEntity);

                    // Success - exit retry loop
                    return;
                }
                else
                {
                    // Processor not found - create new processor
                    _logger.LogInformationWithCorrelation(
                        "Processor not found, creating new processor. CompositeKey: {CompositeKey}",
                        _config.GetCompositeKey());

                    await CreateProcessorAsync();
                    return; // Success - exit retry loop
                }
            }
            catch (RequestTimeoutException ex)
            {
                lastTimeoutException = ex;

                // Record timeout exception metrics
                _healthMetricsService?.RecordException(ex.GetType().Name, "warning", isCritical: false);

                _logger.LogWarningWithCorrelation(ex,
                    "Timeout while requesting processor (attempt {Attempt}/{MaxRetries}). CompositeKey: {CompositeKey}",
                    attempt, maxRetries, _config.GetCompositeKey());

                // If this was the last attempt, we'll throw after the loop
                if (attempt == maxRetries)
                {
                    break; // Exit the retry loop
                }

                // Calculate exponential backoff delay
                var delay = TimeSpan.FromMilliseconds(Math.Min(
                    baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1),
                    maxDelay.TotalMilliseconds));

                _logger.LogInformationWithCorrelation(
                    "Retrying in {DelaySeconds} seconds... (attempt {NextAttempt}/{MaxRetries})",
                    delay.TotalSeconds, attempt + 1, maxRetries);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                activity?.SetErrorTags(ex);

                // Record exception metrics for initialization failures
                _healthMetricsService?.RecordException(ex.GetType().Name, "critical", isCritical: true);

                _logger.LogErrorWithCorrelation(ex,
                    "Failed to initialize processor. CompositeKey: {CompositeKey}",
                    _config.GetCompositeKey());
                throw;
            }
        }

        // If we reach here, all retries failed with timeouts
        _logger.LogErrorWithCorrelation(
            "Failed to initialize processor after {MaxRetries} attempts due to timeouts. CompositeKey: {CompositeKey}",
            maxRetries, _config.GetCompositeKey());

        throw new InvalidOperationException(
            $"Failed to initialize processor after {maxRetries} attempts due to timeout communicating with Processor Manager. CompositeKey: {_config.GetCompositeKey()}",
            lastTimeoutException);
    }

    private async Task CreateProcessorAsync()
    {
        var createCommand = new CreateProcessorCommand
        {
            Version = _config.Version,
            Name = _config.Name,
            Description = _config.Description,
            InputSchemaId = _config.InputSchemaId,
            OutputSchemaId = _config.OutputSchemaId,
            ImplementationHash = GetImplementationHash(),
            RequestedBy = "BaseProcessorApplication"
        };

        _logger.LogDebugWithCorrelation("Publishing CreateProcessorCommand for {CompositeKey} with InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}",
            _config.GetCompositeKey(), createCommand.InputSchemaId, createCommand.OutputSchemaId);

        await _bus.Publish(createCommand);

        // Wait a bit and try to get the processor again
        await Task.Delay(TimeSpan.FromSeconds(2));

        var getQuery = new GetProcessorQuery
        {
            CompositeKey = _config.GetCompositeKey()
        };

        var response = await _bus.Request<GetProcessorQuery, GetProcessorQueryResponse>(
            getQuery, timeout: TimeSpan.FromSeconds(30));

        if (response.Message.Success && response.Message.Entity != null)
        {
            lock (_processorIdLock)
            {
                _processorId = response.Message.Entity.Id;
            }

            _logger.LogInformationWithCorrelation(
                "Successfully created and retrieved processor. ProcessorId: {ProcessorId}, CompositeKey: {CompositeKey}",
                _processorId, _config.GetCompositeKey());

            // Schema definitions already retrieved at initialization start - no need to retrieve again
        }
        else
        {
            throw new InvalidOperationException($"Failed to create or retrieve processor with composite key: {_config.GetCompositeKey()}");
        }
    }

    private async Task RetrieveSchemaDefinitionsAsync()
    {
        _logger.LogInformationWithCorrelation("Retrieving schema definitions for InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}",
            _config.InputSchemaId, _config.OutputSchemaId);

        bool inputSchemaSuccess = false;
        bool outputSchemaSuccess = false;
        string inputErrorMessage = string.Empty;
        string outputErrorMessage = string.Empty;

        try
        {
            // Retrieve input schema definition
            var inputSchemaQuery = new GetSchemaDefinitionQuery
            {
                SchemaId = _config.InputSchemaId,
                RequestedBy = "BaseProcessorApplication"
            };

            var inputSchemaResponse = await _bus.Request<GetSchemaDefinitionQuery, GetSchemaDefinitionQueryResponse>(
                inputSchemaQuery, timeout: TimeSpan.FromSeconds(30));

            if (inputSchemaResponse.Message.Success && !string.IsNullOrEmpty(inputSchemaResponse.Message.Definition))
            {
                _config.InputSchemaDefinition = inputSchemaResponse.Message.Definition;
                inputSchemaSuccess = true;
                _logger.LogInformationWithCorrelation("Successfully retrieved input schema definition. Length: {Length}",
                    _config.InputSchemaDefinition.Length);
            }
            else
            {
                inputErrorMessage = $"Failed to retrieve input schema definition. SchemaId: {_config.InputSchemaId}, Message: {inputSchemaResponse.Message.Message}";
                _logger.LogErrorWithCorrelation(inputErrorMessage);
            }
        }
        catch (Exception ex)
        {
            // Record schema retrieval exception
            _healthMetricsService?.RecordException(ex.GetType().Name, "error", isCritical: true);

            inputErrorMessage = $"Error retrieving input schema definition. SchemaId: {_config.InputSchemaId}, Error: {ex.Message}";
            _logger.LogErrorWithCorrelation(ex, inputErrorMessage);
        }

        try
        {
            // Retrieve output schema definition
            var outputSchemaQuery = new GetSchemaDefinitionQuery
            {
                SchemaId = _config.OutputSchemaId,
                RequestedBy = "BaseProcessorApplication"
            };

            var outputSchemaResponse = await _bus.Request<GetSchemaDefinitionQuery, GetSchemaDefinitionQueryResponse>(
                outputSchemaQuery, timeout: TimeSpan.FromSeconds(30));

            if (outputSchemaResponse.Message.Success && !string.IsNullOrEmpty(outputSchemaResponse.Message.Definition))
            {
                _config.OutputSchemaDefinition = outputSchemaResponse.Message.Definition;
                outputSchemaSuccess = true;
                _logger.LogInformationWithCorrelation("Successfully retrieved output schema definition. Length: {Length}",
                    _config.OutputSchemaDefinition.Length);
            }
            else
            {
                outputErrorMessage = $"Failed to retrieve output schema definition. SchemaId: {_config.OutputSchemaId}, Message: {outputSchemaResponse.Message.Message}";
                _logger.LogErrorWithCorrelation(outputErrorMessage);
            }
        }
        catch (Exception ex)
        {
            // Record schema retrieval exception
            _healthMetricsService?.RecordException(ex.GetType().Name, "error", isCritical: true);

            outputErrorMessage = $"Error retrieving output schema definition. SchemaId: {_config.OutputSchemaId}, Error: {ex.Message}";
            _logger.LogErrorWithCorrelation(ex, outputErrorMessage);
        }

        // Update schema health status
        lock (_schemaHealthLock)
        {
            _inputSchemaHealthy = inputSchemaSuccess;
            _outputSchemaHealthy = outputSchemaSuccess;
            _inputSchemaErrorMessage = inputSchemaSuccess ? string.Empty : inputErrorMessage;
            _outputSchemaErrorMessage = outputSchemaSuccess ? string.Empty : outputErrorMessage;
        }

        // Log overall schema health status
        if (!inputSchemaSuccess || !outputSchemaSuccess)
        {
            _logger.LogErrorWithCorrelation("Processor marked as unhealthy due to schema definition retrieval failures. InputSchemaHealthy: {InputSchemaHealthy}, OutputSchemaHealthy: {OutputSchemaHealthy}",
                inputSchemaSuccess, outputSchemaSuccess);
        }
        else
        {
            _logger.LogInformationWithCorrelation("All schema definitions retrieved successfully. Processor schema health is good.");
        }
    }

    public async Task<Guid> GetProcessorIdAsync()
    {
        if (_processorId.HasValue)
        {
            return _processorId.Value;
        }

        // Check if we're using endless retry mode
        var initConfig = _initializationConfig ?? new ProcessorInitializationConfiguration();

        if (initConfig.RetryEndlessly)
        {
            // In endless retry mode, return empty GUID if not yet initialized
            // The initialization will continue in the background
            _logger.LogDebugWithCorrelation("Processor ID not available yet - initialization in progress or not started");
            return Guid.Empty;
        }

        // Legacy behavior: try to initialize once
        await InitializeAsync();

        if (!_processorId.HasValue)
        {
            throw new InvalidOperationException("Processor ID is not available. Initialization may have failed.");
        }

        return _processorId.Value;
    }

    /// <summary>
    /// Validates that the processor entity's schema IDs match the configured schema IDs
    /// </summary>
    /// <param name="processorEntity">The processor entity retrieved from the query</param>
    /// <returns>True if schema IDs match configuration, false otherwise</returns>
    private bool ValidateSchemaIds(ProcessorEntity processorEntity)
    {
        using var activity = _activitySource.StartActivity("ValidateSchemaIds");
        activity?.SetTag("processor.id", processorEntity.Id.ToString());

        try
        {
            var configInputSchemaId = _config.InputSchemaId;
            var configOutputSchemaId = _config.OutputSchemaId;
            var entityInputSchemaId = processorEntity.InputSchemaId;
            var entityOutputSchemaId = processorEntity.OutputSchemaId;

            activity?.SetTag("config.input_schema_id", configInputSchemaId.ToString())
                    ?.SetTag("config.output_schema_id", configOutputSchemaId.ToString())
                    ?.SetTag("entity.input_schema_id", entityInputSchemaId.ToString())
                    ?.SetTag("entity.output_schema_id", entityOutputSchemaId.ToString());

            bool inputSchemaMatches = configInputSchemaId == entityInputSchemaId;
            bool outputSchemaMatches = configOutputSchemaId == entityOutputSchemaId;
            bool allSchemasValid = inputSchemaMatches && outputSchemaMatches;

            string validationMessage = string.Empty;
            if (!allSchemasValid)
            {
                var errors = new List<string>();
                if (!inputSchemaMatches)
                {
                    errors.Add($"Input schema mismatch: Config={configInputSchemaId}, Entity={entityInputSchemaId}");
                }
                if (!outputSchemaMatches)
                {
                    errors.Add($"Output schema mismatch: Config={configOutputSchemaId}, Entity={entityOutputSchemaId}");
                }
                validationMessage = string.Join("; ", errors);
            }

            // Update schema validation status
            lock (_schemaHealthLock)
            {
                _schemaIdsValid = allSchemasValid;
                _schemaValidationErrorMessage = allSchemasValid ? string.Empty : validationMessage;
            }

            if (allSchemasValid)
            {
                _logger.LogInformationWithCorrelation(
                    "Schema ID validation successful. ProcessorId: {ProcessorId}, " +
                    "InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}",
                    processorEntity.Id, configInputSchemaId, configOutputSchemaId);
            }
            else
            {
                _logger.LogErrorWithCorrelation(
                    "Schema ID validation failed. ProcessorId: {ProcessorId}, ValidationErrors: {ValidationErrors}",
                    processorEntity.Id, validationMessage);
            }

            activity?.SetTag("validation.success", allSchemasValid)
                    ?.SetTag("validation.input_match", inputSchemaMatches)
                    ?.SetTag("validation.output_match", outputSchemaMatches);

            return allSchemasValid;
        }
        catch (Exception ex)
        {
            // Record schema validation exception
            _healthMetricsService?.RecordException(ex.GetType().Name, "error", isCritical: true);

            var errorMessage = $"Error during schema ID validation: {ex.Message}";

            lock (_schemaHealthLock)
            {
                _schemaIdsValid = false;
                _schemaValidationErrorMessage = errorMessage;
            }

            _logger.LogErrorWithCorrelation(ex, "Schema ID validation failed with exception. ProcessorId: {ProcessorId}",
                processorEntity.Id);

            activity?.SetTag("validation.success", false)
                    ?.SetTag("validation.error", ex.Message);

            return false;
        }
    }

    /// <summary>
    /// Gets the current schema health status including schema ID validation
    /// </summary>
    /// <returns>A tuple indicating if schemas are healthy and valid</returns>
    public (bool InputSchemaHealthy, bool OutputSchemaHealthy, bool SchemaIdsValid, string InputSchemaError, string OutputSchemaError, string SchemaValidationError) GetSchemaHealthStatus()
    {
        lock (_schemaHealthLock)
        {
            return (_inputSchemaHealthy, _outputSchemaHealthy, _schemaIdsValid, _inputSchemaErrorMessage, _outputSchemaErrorMessage, _schemaValidationErrorMessage);
        }
    }

    public string GetCacheMapName()
    {
        if (!_processorId.HasValue)
        {
            throw new InvalidOperationException("Processor ID is not available. Call InitializeAsync first.");
        }
        return _processorId.Value.ToString();
    }

    public async Task<string?> GetCachedDataAsync(Guid orchestratedFlowEntityId, Guid correlationId, Guid executionId, Guid stepId , Guid publishId )
    {
        var mapName = GetCacheMapName();
        var key = _cacheService.GetCacheKey(orchestratedFlowEntityId, correlationId, executionId, stepId, publishId);

        _logger.LogDebugWithCorrelation("Retrieving cached data. MapName: {MapName}, ExecutionId: {ExecutionId}, StepId: {StepId}, OrchestratedFlowEntityId: {OrchestratedFlowEntityId}",
            mapName, executionId, stepId, orchestratedFlowEntityId);

        var result = await _cacheService.GetAsync(mapName, key);

        _logger.LogDebugWithCorrelation("Cache retrieval result. MapName: {MapName}, ExecutionId: {ExecutionId}, Found: {Found}, DataLength: {DataLength}",
            mapName, executionId, result != null, result?.Length ?? 0);

        return result;
    }

    public async Task SaveCachedDataAsync(Guid orchestratedFlowEntityId, Guid correlationId, Guid executionId, Guid stepId , Guid publishId, string data)
    {
        var mapName = GetCacheMapName();
        var key = _cacheService.GetCacheKey(orchestratedFlowEntityId, correlationId, executionId, stepId, publishId);

        _logger.LogDebugWithCorrelation("Saving cached data. MapName: {MapName}, ExecutionId: {ExecutionId}, StepId: {StepId}, OrchestratedFlowEntityId: {OrchestratedFlowEntityId}, DataLength: {DataLength}",
            mapName, executionId, stepId, orchestratedFlowEntityId, data.Length);

        await _cacheService.SetAsync(mapName, key, data);

        _logger.LogDebugWithCorrelation("Successfully saved cached data. MapName: {MapName}, ExecutionId: {ExecutionId}",
            mapName, executionId);
    }

    public async Task<bool> ValidateInputDataAsync(string data)
    {
        if (!_validationConfig.EnableInputValidation)
        {
            return true;
        }

        if (string.IsNullOrEmpty(_config.InputSchemaDefinition))
        {
            _logger.LogWarningWithCorrelation("Input schema definition is not available. Skipping validation.");
            return false;
        }

        return await _schemaValidator.ValidateAsync(data, _config.InputSchemaDefinition);
    }

    public async Task<bool> ValidateOutputDataAsync(string data)
    {
        if (!_validationConfig.EnableOutputValidation)
        {
            return true;
        }

        if (string.IsNullOrEmpty(_config.OutputSchemaDefinition))
        {
            _logger.LogWarningWithCorrelation("Output schema definition is not available. Skipping validation.");
            return false;
        }

        return await _schemaValidator.ValidateAsync(data, _config.OutputSchemaDefinition);
    }

    public async Task<IEnumerable<ProcessorActivityResponse>> ProcessActivityAsync(ProcessorActivityMessage message)
    {
        using var activity = _activitySource.StartActivityWithCorrelation("ProcessActivity");
        var stopwatch = Stopwatch.StartNew();

        activity?.SetActivityExecutionTagsWithCorrelation(
            message.OrchestratedFlowEntityId,
            message.StepId,
            message.ExecutionId)
            ?.SetEntityTags(message.Entities.Count);

        _logger.LogInformationWithCorrelation(
            "Processing activity. ProcessorId: {ProcessorId}, OrchestratedFlowEntityId: {OrchestratedFlowEntityId}, StepId: {StepId}, ExecutionId: {ExecutionId}, EntitiesCount: {EntitiesCount}",
            message.ProcessorId, message.OrchestratedFlowEntityId, message.StepId, message.ExecutionId, message.Entities.Count);

        try
        {
            string inputData;

            // Handle special case when ExecutionId is empty
            if (message.ExecutionId == Guid.Empty)
            {
                _logger.LogInformationWithCorrelation(
                    "ExecutionId is empty - skipping cache retrieval and input validation. ProcessorId: {ProcessorId}, OrchestratedFlowEntityId: {OrchestratedFlowEntityId}, StepId: {StepId}",
                    message.ProcessorId, message.OrchestratedFlowEntityId, message.StepId);

                // Skip cache retrieval and use empty string as input data
                inputData = string.Empty;
            }
            else
            {
                // 1. Retrieve data from cache (normal case)
                inputData = await GetCachedDataAsync(
                    message.OrchestratedFlowEntityId,
                    message.CorrelationId,
                    message.ExecutionId,
                    message.StepId,
                    message.PublishId) ?? string.Empty;

                if (string.IsNullOrEmpty(inputData))
                {
                    _logger.LogErrorWithCorrelation(
                        "No input data found in cache. ProcessorId: {ProcessorId}, ExecutionId: {ExecutionId}, StepId: {StepId}, OrchestratedFlowEntityId: {OrchestratedFlowEntityId}, PublishId: {PublishId}, CacheKey: {CacheKey}",
                        message.ProcessorId, message.ExecutionId, message.StepId, message.OrchestratedFlowEntityId, message.PublishId,
                        _cacheService.GetCacheKey(message.OrchestratedFlowEntityId, message.CorrelationId, message.ExecutionId, message.StepId, message.PublishId));

                    throw new InvalidOperationException(
                        $"No input data found in cache for ExecutionId: {message.ExecutionId}, StepId: {message.StepId}");
                }

                // 2. Validate input data against InputSchema (normal case)
                if (!await ValidateInputDataAsync(inputData))
                {
                    var errorMessage = "Input data validation failed against InputSchema";
                    _logger.LogErrorWithCorrelation(
                        "{ErrorMessage}. ProcessorId: {ProcessorId}, ExecutionId: {ExecutionId}, StepId: {StepId}, OrchestratedFlowEntityId: {OrchestratedFlowEntityId}",
                        errorMessage, message.ProcessorId, message.ExecutionId, message.StepId, message.OrchestratedFlowEntityId);

                    if (_validationConfig.FailOnValidationError)
                    {
                        throw new InvalidOperationException($"{errorMessage} for ExecutionId: {message.ExecutionId}");
                    }
                }
            }

            // Validate all validatable assignment entities by their schemas
            await ValidateAssignmentEntitiesAsync(message.Entities);

            // 3. Execute the activity

            var resultDataCollection = await _activityExecutor.ExecuteActivityAsync(
                message.ProcessorId,
                message.OrchestratedFlowEntityId,
                message.StepId,
                message.ExecutionId,
                message.Entities,
                inputData,
                message.CorrelationId);

            var responses = new List<ProcessorActivityResponse>();

            // Process each result item
            foreach (var resultData in resultDataCollection)
            {
                try
                {
                    // 5. Validate output data against OutputSchema for this item
                    if (!await ValidateOutputDataAsync(resultData.SerializedData))
                    {
                        var errorMessage = "Output data validation failed against OutputSchema";
                        _logger.LogErrorWithCorrelation(
                            "{ErrorMessage}. ProcessorId: {ProcessorId}, ExecutionId: {ExecutionId}, StepId: {StepId}, OrchestratedFlowEntityId: {OrchestratedFlowEntityId}",
                            errorMessage, message.ProcessorId, resultData.ExecutionId, message.StepId, message.OrchestratedFlowEntityId);

                        if (_validationConfig.FailOnValidationError)
                        {
                            throw new InvalidOperationException($"{errorMessage} for ExecutionId: {resultData.ExecutionId}");
                        }
                    }

                    // 6. Save result data to cache (skip if final ExecutionId is empty)
                    if (resultData.ExecutionId != Guid.Empty)
                    {
                        await SaveCachedDataAsync(
                            message.OrchestratedFlowEntityId,
                            message.CorrelationId,
                            resultData.ExecutionId,
                            message.StepId,
                            message.PublishId,
                            resultData.SerializedData);
                    }
                    else
                    {
                        _logger.LogWarningWithCorrelation(
                            "ExecutionId is empty - skipping cache save. ProcessorId: {ProcessorId}, OrchestratedFlowEntityId: {OrchestratedFlowEntityId}, StepId: {StepId}, OriginalExecutionId: {OriginalExecutionId}",
                            message.ProcessorId, message.OrchestratedFlowEntityId, message.StepId, message.ExecutionId);
                    }

                    // Create successful response for this item
                    var response = new ProcessorActivityResponse
                    {
                        ProcessorId = message.ProcessorId,
                        OrchestratedFlowEntityId = message.OrchestratedFlowEntityId,
                        StepId = message.StepId,
                        ExecutionId = resultData.ExecutionId,
                        Status = ActivityExecutionStatus.Completed,
                        CorrelationId = message.CorrelationId,
                        Duration = stopwatch.Elapsed
                    };

                    responses.Add(response);

                    _logger.LogInformationWithCorrelation(
                        "Successfully processed activity item. ProcessorId: {ProcessorId}, OrchestratedFlowEntityId: {OrchestratedFlowEntityId}, StepId: {StepId}, ExecutionId: {ExecutionId}",
                        message.ProcessorId, message.OrchestratedFlowEntityId, message.StepId, resultData.ExecutionId);
                }
                catch (Exception itemEx)
                {
                    _logger.LogErrorWithCorrelation(itemEx,
                        "Failed to process activity item. ProcessorId: {ProcessorId}, OrchestratedFlowEntityId: {OrchestratedFlowEntityId}, StepId: {StepId}, ExecutionId: {ExecutionId}",
                        message.ProcessorId, message.OrchestratedFlowEntityId, message.StepId, resultData.ExecutionId);

                    // Create failed response for this item
                    var failedResponse = new ProcessorActivityResponse
                    {
                        ProcessorId = message.ProcessorId,
                        OrchestratedFlowEntityId = message.OrchestratedFlowEntityId,
                        StepId = message.StepId,
                        ExecutionId = resultData.ExecutionId,
                        Status = ActivityExecutionStatus.Failed,
                        CorrelationId = message.CorrelationId,
                        ErrorMessage = itemEx.Message,
                        Duration = stopwatch.Elapsed
                    };

                    responses.Add(failedResponse);
                }
            }

            stopwatch.Stop();

            // Record performance metrics if available
            _performanceMetricsService?.RecordActivity(true, stopwatch.Elapsed.TotalMilliseconds);

            activity?.SetTag(ActivityTags.ActivityStatus, ActivityExecutionStatus.Completed.ToString())
                    ?.SetTag(ActivityTags.ActivityDuration, stopwatch.ElapsedMilliseconds);

            _logger.LogInformationWithCorrelation(
                "Successfully processed activity collection. ProcessorId: {ProcessorId}, OrchestratedFlowEntityId: {OrchestratedFlowEntityId}, StepId: {StepId}, ItemCount: {ItemCount}, Duration: {Duration}ms",
                message.ProcessorId, message.OrchestratedFlowEntityId, message.StepId, responses.Count, stopwatch.ElapsedMilliseconds);

            return responses;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record performance metrics if available
            _performanceMetricsService?.RecordActivity(false, stopwatch.Elapsed.TotalMilliseconds);

            // Record exception metrics
            _healthMetricsService?.RecordException(ex.GetType().Name, "error", isCritical: true);

            activity?.SetErrorTags(ex)
                    ?.SetTag(ActivityTags.ActivityStatus, ActivityExecutionStatus.Failed.ToString())
                    ?.SetTag(ActivityTags.ActivityDuration, stopwatch.ElapsedMilliseconds);

            _logger.LogErrorWithCorrelation(ex,
                "Failed to process activity. ProcessorId: {ProcessorId}, OrchestratedFlowEntityId: {OrchestratedFlowEntityId}, StepId: {StepId}, ExecutionId: {ExecutionId}, Duration: {Duration}ms",
                message.ProcessorId, message.OrchestratedFlowEntityId, message.StepId, message.ExecutionId, stopwatch.ElapsedMilliseconds);

            var processorId = _processorId ?? Guid.Empty;
            return new[]
            {
                new ProcessorActivityResponse
                {
                    ProcessorId = processorId,
                    OrchestratedFlowEntityId = message.OrchestratedFlowEntityId,
                    StepId = message.StepId,
                    ExecutionId = message.ExecutionId,
                    Status = ActivityExecutionStatus.Failed,
                    CorrelationId = message.CorrelationId,
                    ErrorMessage = ex.Message,
                    Duration = stopwatch.Elapsed
                }
            };
        }
    }

    public async Task<ProcessorHealthResponse> GetHealthStatusAsync()
    {
        using var activity = _activitySource.StartActivity("GetHealthStatus");
        var processorId = await GetProcessorIdAsync();

        activity?.SetProcessorTags(processorId, _config.Name, _config.Version);

        try
        {
            var healthChecks = new Dictionary<string, HealthCheckResult>();

            // Check initialization status first
            bool isInitialized, isInitializing;
            string initializationError;
            lock (_initializationLock)
            {
                isInitialized = _isInitialized;
                isInitializing = _isInitializing;
                initializationError = _initializationErrorMessage;
            }

            healthChecks["initialization"] = new HealthCheckResult
            {
                Status = isInitialized ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                Description = "Processor initialization status",
                Data = new Dictionary<string, object>
                {
                    ["initialized"] = isInitialized,
                    ["initializing"] = isInitializing,
                    ["error_message"] = initializationError
                }
            };

            // Check cache health
            var cacheHealthy = await _cacheService.IsHealthyAsync();
            healthChecks["cache"] = new HealthCheckResult
            {
                Status = cacheHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                Description = "Hazelcast cache connectivity",
                Data = new Dictionary<string, object> { ["connected"] = cacheHealthy }
            };

            // Check message bus health (basic check)
            var busHealthy = _bus != null;
            healthChecks["messagebus"] = new HealthCheckResult
            {
                Status = busHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                Description = "MassTransit message bus connectivity",
                Data = new Dictionary<string, object> { ["connected"] = busHealthy }
            };

            // Check schema health including schema ID validation and implementation hash validation
            bool inputSchemaHealthy, outputSchemaHealthy, schemaIdsValid, implementationHashValid;
            string inputSchemaError, outputSchemaError, schemaValidationError, implementationHashError;

            lock (_schemaHealthLock)
            {
                inputSchemaHealthy = _inputSchemaHealthy;
                outputSchemaHealthy = _outputSchemaHealthy;
                schemaIdsValid = _schemaIdsValid;
                inputSchemaError = _inputSchemaErrorMessage;
                outputSchemaError = _outputSchemaErrorMessage;
                schemaValidationError = _schemaValidationErrorMessage;
                implementationHashValid = _implementationHashValid;
                implementationHashError = _implementationHashErrorMessage;
            }

            healthChecks["schema_validation"] = new HealthCheckResult
            {
                Status = schemaIdsValid ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                Description = "Schema ID validation against processor configuration",
                Data = new Dictionary<string, object>
                {
                    ["valid"] = schemaIdsValid,
                    ["config_input_schema_id"] = _config.InputSchemaId.ToString(),
                    ["config_output_schema_id"] = _config.OutputSchemaId.ToString(),
                    ["validation_error"] = schemaValidationError
                }
            };

            healthChecks["input_schema"] = new HealthCheckResult
            {
                Status = inputSchemaHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                Description = "Input schema definition availability",
                Data = new Dictionary<string, object>
                {
                    ["available"] = inputSchemaHealthy,
                    ["schema_id"] = _config.InputSchemaId.ToString(),
                    ["error_message"] = inputSchemaError
                }
            };

            healthChecks["output_schema"] = new HealthCheckResult
            {
                Status = outputSchemaHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                Description = "Output schema definition availability",
                Data = new Dictionary<string, object>
                {
                    ["available"] = outputSchemaHealthy,
                    ["schema_id"] = _config.OutputSchemaId.ToString(),
                    ["error_message"] = outputSchemaError
                }
            };

            healthChecks["implementation_hash"] = new HealthCheckResult
            {
                Status = implementationHashValid ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                Description = "Implementation hash validation for version integrity",
                Data = new Dictionary<string, object>
                {
                    ["valid"] = implementationHashValid,
                    ["processor_version"] = _config.Version,
                    ["validation_error"] = implementationHashError
                }
            };

            var overallStatus = healthChecks.Values.All(h => h.Status == HealthStatus.Healthy)
                ? HealthStatus.Healthy
                : HealthStatus.Unhealthy;

            // Create detailed message based on health status
            string healthMessage;
            if (overallStatus == HealthStatus.Healthy)
            {
                healthMessage = "All systems operational";
            }
            else
            {
                var unhealthyComponents = healthChecks
                    .Where(h => h.Value.Status != HealthStatus.Healthy)
                    .Select(h => h.Key)
                    .ToList();

                if (!isInitialized)
                {
                    if (isInitializing)
                    {
                        healthMessage = "Processor is initializing";
                    }
                    else
                    {
                        healthMessage = string.IsNullOrEmpty(initializationError)
                            ? "Processor not yet initialized"
                            : $"Processor initialization failed: {initializationError}";
                    }
                }
                else
                {
                    healthMessage = $"Processor is unhealthy. Failed components: {string.Join(", ", unhealthyComponents)}";

                    // Add specific error details if components are unhealthy
                    var errorDetails = new List<string>();
                    if (!schemaIdsValid) errorDetails.Add($"Schema validation: {schemaValidationError}");
                    if (!inputSchemaHealthy) errorDetails.Add($"Input schema: {inputSchemaError}");
                    if (!outputSchemaHealthy) errorDetails.Add($"Output schema: {outputSchemaError}");
                    if (!implementationHashValid) errorDetails.Add($"Implementation hash: {implementationHashError}");

                    if (errorDetails.Any())
                    {
                        healthMessage += $". Error details: {string.Join("; ", errorDetails)}";
                    }
                }
            }

            return new ProcessorHealthResponse
            {
                ProcessorId = processorId,
                Status = overallStatus,
                Message = healthMessage,
                HealthCheckInterval = GetHealthCheckIntervalFromConfig(),
                HealthChecks = healthChecks,
                Uptime = DateTime.UtcNow - _startTime,
                Metadata = new ProcessorMetadata
                {
                    Version = _config.Version,
                    Name = _config.Name,
                    StartTime = _startTime,
                    HostName = Environment.MachineName,
                    ProcessId = Environment.ProcessId,
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                }
            };
        }
        catch (Exception ex)
        {
            activity?.SetErrorTags(ex);

            // Record health status retrieval exception
            _healthMetricsService?.RecordException(ex.GetType().Name, "error", isCritical: false);

            _logger.LogErrorWithCorrelation(ex, "Failed to get health status for ProcessorId: {ProcessorId}", processorId);

            return new ProcessorHealthResponse
            {
                ProcessorId = processorId,
                Status = HealthStatus.Unhealthy,
                Message = $"Health check failed: {ex.Message}",
                HealthCheckInterval = GetHealthCheckIntervalFromConfig(),
                Uptime = DateTime.UtcNow - _startTime,
                Metadata = new ProcessorMetadata
                {
                    Version = _config.Version,
                    Name = _config.Name,
                    StartTime = _startTime,
                    HostName = Environment.MachineName,
                    ProcessId = Environment.ProcessId,
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                }
            };
        }
    }

    public async Task<ProcessorStatisticsResponse> GetStatisticsAsync(DateTime? startTime, DateTime? endTime)
    {
        using var activity = _activitySource.StartActivity("GetStatistics");
        var processorId = await GetProcessorIdAsync();

        activity?.SetProcessorTags(processorId, _config.Name, _config.Version);

        try
        {
            // For now, return basic metrics
            // In a production system, you might want to store more detailed statistics
            var periodStart = startTime ?? _startTime;
            var periodEnd = endTime ?? DateTime.UtcNow;

            return new ProcessorStatisticsResponse
            {
                ProcessorId = processorId,
                TotalActivitiesProcessed = 0, // Would need to implement proper tracking
                SuccessfulActivities = 0,
                FailedActivities = 0,
                AverageExecutionTime = TimeSpan.Zero,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                CollectedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            activity?.SetErrorTags(ex);

            // Record statistics retrieval exception
            _healthMetricsService?.RecordException(ex.GetType().Name, "error", isCritical: false);

            _logger.LogErrorWithCorrelation(ex, "Failed to get statistics for ProcessorId: {ProcessorId}", processorId);
            throw;
        }
    }

    /// <summary>
    /// Gets the implementation hash for the current processor using reflection
    /// </summary>
    /// <returns>The SHA-256 hash of the processor implementation</returns>
    private string GetImplementationHash()
    {
        try
        {
            // Use reflection to find the ProcessorImplementationHash class in the entry assembly
            var entryAssembly = System.Reflection.Assembly.GetEntryAssembly();
            if (entryAssembly == null)
            {
                _logger.LogWarningWithCorrelation("Entry assembly not found. Using empty implementation hash.");
                return string.Empty;
            }

            var hashType = entryAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "ProcessorImplementationHash");

            if (hashType == null)
            {
                _logger.LogWarningWithCorrelation("ProcessorImplementationHash class not found in entry assembly. Using empty implementation hash.");
                return string.Empty;
            }

            // Try to get Hash as a property first, then as a field
            var hashProperty = hashType.GetProperty("Hash", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            string hash = string.Empty;

            if (hashProperty != null)
            {
                hash = hashProperty.GetValue(null) as string ?? string.Empty;
                _logger.LogDebugWithCorrelation("Retrieved implementation hash from property: {Hash}", hash);
            }
            else
            {
                // Try to get Hash as a field (const field)
                var hashField = hashType.GetField("Hash", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (hashField != null)
                {
                    hash = hashField.GetValue(null) as string ?? string.Empty;
                    _logger.LogDebugWithCorrelation("Retrieved implementation hash from field: {Hash}", hash);
                }
                else
                {
                    _logger.LogWarningWithCorrelation("Hash property or field not found in ProcessorImplementationHash class. Using empty implementation hash.");
                    return string.Empty;
                }
            }
            _logger.LogInformationWithCorrelation("Retrieved implementation hash: {Hash}", hash);
            return hash;
        }
        catch (Exception ex)
        {
            // Record implementation hash retrieval exception
            _healthMetricsService?.RecordException(ex.GetType().Name, "warning", isCritical: false);

            _logger.LogErrorWithCorrelation(ex, "Error retrieving implementation hash. Using empty hash.");
            return string.Empty;
        }
    }

    /// <summary>
    /// Validates that the processor entity's implementation hash matches the current implementation
    /// </summary>
    /// <param name="processorEntity">The processor entity retrieved from the query</param>
    /// <returns>True if implementation hashes match, false otherwise</returns>
    private bool ValidateImplementationHash(ProcessorEntity processorEntity)
    {
        using var activity = _activitySource.StartActivity("ValidateImplementationHash");
        activity?.SetTag("processor.id", processorEntity.Id.ToString());

        try
        {
            var currentHash = GetImplementationHash();
            var storedHash = processorEntity.ImplementationHash ?? string.Empty;

            activity?.SetTag("current.hash", currentHash)
                    ?.SetTag("stored.hash", storedHash);

            // If current hash is empty (couldn't retrieve), skip validation
            if (string.IsNullOrEmpty(currentHash))
            {
                _logger.LogWarningWithCorrelation(
                    "Current implementation hash is empty, skipping hash validation. ProcessorId: {ProcessorId}",
                    processorEntity.Id);
                return true;
            }

            // If stored hash is empty, this is an old processor without hash - allow it
            if (string.IsNullOrEmpty(storedHash))
            {
                _logger.LogInformationWithCorrelation(
                    "Stored implementation hash is empty (legacy processor), allowing initialization. ProcessorId: {ProcessorId}",
                    processorEntity.Id);
                return true;
            }

            bool hashesMatch = currentHash.Equals(storedHash, StringComparison.OrdinalIgnoreCase);

            // Update implementation hash validation status
            string validationErrorMessage = string.Empty;
            if (!hashesMatch)
            {
                validationErrorMessage = $"Implementation hash mismatch: Expected={storedHash}, Actual={currentHash}. Version increment required for processor {_config.GetCompositeKey()}.";
            }

            lock (_schemaHealthLock)
            {
                _implementationHashValid = hashesMatch;
                _implementationHashErrorMessage = hashesMatch ? string.Empty : validationErrorMessage;
            }

            if (hashesMatch)
            {
                _logger.LogInformationWithCorrelation(
                    "Implementation hash validation successful. ProcessorId: {ProcessorId}, Hash: {Hash}",
                    processorEntity.Id, currentHash);
            }
            else
            {
                _logger.LogErrorWithCorrelation(
                    "Implementation hash validation failed. ProcessorId: {ProcessorId}, " +
                    "Expected: {ExpectedHash}, Actual: {ActualHash}. " +
                    "Version increment required for processor {CompositeKey}.",
                    processorEntity.Id, storedHash, currentHash, _config.GetCompositeKey());
            }

            activity?.SetTag("validation.success", hashesMatch);
            return hashesMatch;
        }
        catch (Exception ex)
        {
            // Record implementation hash validation exception
            _healthMetricsService?.RecordException(ex.GetType().Name, "error", isCritical: true);

            var errorMessage = $"Error during implementation hash validation: {ex.Message}";

            // Update implementation hash validation status for exception case
            lock (_schemaHealthLock)
            {
                _implementationHashValid = false;
                _implementationHashErrorMessage = errorMessage;
            }

            _logger.LogErrorWithCorrelation(ex, "Implementation hash validation failed with exception. ProcessorId: {ProcessorId}",
                processorEntity.Id);

            activity?.SetTag("validation.success", false)
                    ?.SetTag("validation.error", ex.Message);

            return false;
        }
    }

    /// <summary>
    /// Gets the health check interval from configuration in seconds
    /// </summary>
    /// <returns>Health check interval in seconds, defaults to 30 if not configured</returns>
    private int GetHealthCheckIntervalFromConfig()
    {
        try
        {
            var healthCheckIntervalString = _configuration["ProcessorHealthMonitor:HealthCheckInterval"];
            if (string.IsNullOrEmpty(healthCheckIntervalString))
            {
                return 30; // Default value
            }

            if (TimeSpan.TryParse(healthCheckIntervalString, out var interval))
            {
                return (int)interval.TotalSeconds;
            }

            return 30; // Default value if parsing fails
        }
        catch
        {
            return 30; // Default value if any exception occurs
        }
    }

    /// <summary>
    /// Validates all validatable assignment entities by their payload schemas
    /// </summary>
    /// <param name="entities">List of assignment models to validate</param>
    private async Task ValidateAssignmentEntitiesAsync(List<AssignmentModel> entities)
    {
        if (!_validationConfig.EnableInputValidation)
        {
            _logger.LogDebugWithCorrelation("Input validation is disabled. Skipping assignment entity validation.");
            return;
        }

        // Extract all validatable entities using the interface
        var validatableEntities = entities
            .OfType<IValidatableAssignmentModel>()
            .ToList();

        if (!validatableEntities.Any())
        {
            _logger.LogDebugWithCorrelation("No validatable assignment entities found. Skipping validation.");
            return;
        }

        _logger.LogInformationWithCorrelation("Validating {Count} assignment entities by their schemas.", validatableEntities.Count);

        foreach (var assignmentEntity in validatableEntities)
        {
            await ValidateEntityPayloadAsync(assignmentEntity.EntityId, assignmentEntity.GetValidatableModel());
        }
    }

    /// <summary>
    /// Validates a single entity's payload against its schema
    /// </summary>
    /// <param name="entityId">The entity ID for logging purposes</param>
    /// <param name="model">The validatable model containing payload and schema data</param>
    private async Task ValidateEntityPayloadAsync(Guid entityId, IValidatableModel model)
    {
        try
        {
            if (string.IsNullOrEmpty(model.SchemaDefinition))
            {
                _logger.LogWarningWithCorrelation("Schema definition is missing for entity. EntityId: {EntityId}, Name: {Name}, Version: {Version}",
                    entityId, model.Name, model.Version);

                if (_validationConfig.FailOnValidationError)
                {
                    throw new InvalidOperationException($"Schema definition is missing for entity {entityId}");
                }
                return;
            }

            if (string.IsNullOrEmpty(model.Payload))
            {
                _logger.LogWarningWithCorrelation("Payload is empty for entity. EntityId: {EntityId}, Name: {Name}, Version: {Version}",
                    entityId, model.Name, model.Version);

                if (_validationConfig.FailOnValidationError)
                {
                    throw new InvalidOperationException($"Payload is empty for entity {entityId}");
                }
                return;
            }

            // Validate payload against schema
            var isValid = await _schemaValidator.ValidateAsync(model.Payload, model.SchemaDefinition);

            if (!isValid)
            {
                var errorMessage = $"Payload validation failed against schema for entity {entityId}";
                _logger.LogErrorWithCorrelation("{ErrorMessage}. EntityId: {EntityId}, Name: {Name}, Version: {Version}",
                    errorMessage, entityId, model.Name, model.Version);

                if (_validationConfig.FailOnValidationError)
                {
                    throw new InvalidOperationException(errorMessage);
                }
            }
            else
            {
                _logger.LogDebugWithCorrelation("Payload validation passed. EntityId: {EntityId}, Name: {Name}, Version: {Version}",
                    entityId, model.Name, model.Version);
            }
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            var errorMessage = $"Error validating entity {entityId}: {ex.Message}";
            _logger.LogErrorWithCorrelation(ex, "{ErrorMessage}", errorMessage);

            if (_validationConfig.FailOnValidationError)
            {
                throw new InvalidOperationException(errorMessage, ex);
            }
        }
    }
}
