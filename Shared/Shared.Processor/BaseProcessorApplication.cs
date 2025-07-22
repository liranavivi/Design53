using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Configuration;
using Shared.Correlation;
using Shared.Models;
using Shared.Processor.MassTransit.Consumers;
using Shared.Processor.Models;
using Shared.Processor.Services;
using System.Diagnostics;
using System.Text.Json;

namespace Shared.Processor.Application;

/// <summary>
/// Abstract base class for processor applications
/// </summary>
public abstract class BaseProcessorApplication : IActivityExecutor
{
    private IHost? _host;
    private ILogger<BaseProcessorApplication>? _logger;
    private ProcessorConfiguration? _config;

    /// <summary>
    /// Protected property to access the service provider for derived classes
    /// </summary>
    protected IServiceProvider ServiceProvider => _host?.Services ?? throw new InvalidOperationException("Host not initialized");

    /// <summary>
    /// Main implementation of activity execution that handles common patterns
    /// </summary>
    public virtual async Task<IEnumerable<ActivityExecutionResult>> ExecuteActivityAsync(
        Guid processorId,
        Guid orchestratedFlowEntityId,
        Guid stepId,
        Guid executionId,
        List<AssignmentModel> entities,
        string inputData,
        Guid correlationId ,
        CancellationToken cancellationToken = default)
    {
        var logger = ServiceProvider.GetRequiredService<ILogger<BaseProcessorApplication>>();
        var stopwatch = Stopwatch.StartNew();
        var results = new List<ActivityExecutionResult>();

        try
        {
            var processedDataCollection = await ProcessActivityDataAsync(
                processorId,
                orchestratedFlowEntityId,
                stepId,
                executionId,
                entities,
                inputData, // Raw input data - concrete processor parses and validates
                correlationId,
                cancellationToken);

            stopwatch.Stop();

            // Process each ProcessedActivityData item
            foreach (var processedData in processedDataCollection)
            {
                try
                {
                    // Serialize only the Data property to JSON for this item
                    var serializedData = JsonSerializer.Serialize(processedData.Data, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true
                    });

                    // Create ActivityExecutionResult for this item
                    var result = new ActivityExecutionResult
                    {
                        Result = processedData.Result ?? "Processing completed successfully",
                        Status = processedData.Status ?? ActivityExecutionStatus.Completed,
                        Duration = stopwatch.Elapsed,
                        ProcessorName = processedData.ProcessorName ?? GetType().Name,
                        Version = processedData.Version ?? "1.0",
                        ExecutionId = processedData.ExecutionId == Guid.Empty ? Guid.NewGuid() : processedData.ExecutionId,
                        SerializedData = serializedData
                    };

                    results.Add(result);
                }
                catch (Exception itemEx)
                {
                    logger.LogErrorWithCorrelation(itemEx, "Failed to process individual item. ExecutionId: {ExecutionId}", processedData.ExecutionId);

                    // Record exception metrics for this item
                    var healthMetricsService = ServiceProvider?.GetService<IProcessorHealthMetricsService>();
                    healthMetricsService?.RecordException(itemEx.GetType().Name, "error", isCritical: false);

                    // Add failed result for this item
                    var failedResult = new ActivityExecutionResult
                    {
                        Result = $"Item processing failed: {itemEx.Message}",
                        Status = ActivityExecutionStatus.Failed,
                        Duration = stopwatch.Elapsed,
                        ProcessorName = GetType().Name,
                        Version = "1.0",
                        ExecutionId = processedData.ExecutionId == Guid.Empty ? Guid.NewGuid() : processedData.ExecutionId,
                        SerializedData = "{}" // Empty JSON object for failed processing
                    };

                    results.Add(failedResult);
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogErrorWithCorrelation(ex, "Activity execution failed completely");

            // Record exception metrics
            var healthMetricsService = ServiceProvider?.GetService<IProcessorHealthMetricsService>();
            healthMetricsService?.RecordException(ex.GetType().Name, "error", isCritical: true);

            // Return single failed result when entire processing fails
            return new[]
            {
                new ActivityExecutionResult
                {
                    Result = $"Processing failed: {ex.Message}",
                    Status = ActivityExecutionStatus.Failed,
                    Duration = stopwatch.Elapsed,
                    ProcessorName = GetType().Name,
                    Version = "1.0",
                    ExecutionId = executionId,
                    SerializedData = "{}" // Empty JSON object for failed processing
                }
            };
        }
    }

    /// <summary>
    /// Abstract method that concrete processor implementations must override
    /// This is where the specific processor business logic should be implemented
    /// Returns a collection of ProcessedActivityData, each with a unique ExecutionId
    /// </summary>
    /// <param name="processorId">ID of the processor executing the activity</param>
    /// <param name="orchestratedFlowEntityId">ID of the orchestrated flow entity</param>
    /// <param name="stepId">ID of the step being executed</param>
    /// <param name="executionId">Original execution ID for this activity instance</param>
    /// <param name="entities">Collection of base entities to process</param>
    /// <param name="inputData">Parsed input data object</param>
    /// <param name="correlationId">Optional correlation ID for tracking</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Collection of processed data, each with unique ExecutionId, that will be incorporated into the standard result structure</returns>
    protected abstract Task<IEnumerable<ProcessedActivityData>> ProcessActivityDataAsync(
        Guid processorId,
        Guid orchestratedFlowEntityId,
        Guid stepId,
        Guid executionId,
        List<AssignmentModel> entities,
        string inputData,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Data structure for returning processed activity data
    /// </summary>
    protected class ProcessedActivityData
    {
        public string? Result { get; set; }
        public ActivityExecutionStatus? Status { get; set; }
        public object? Data { get; set; }
        public string? ProcessorName { get; set; }
        public string? Version { get; set; }
        public Guid ExecutionId { get; set; }
    }

    /// <summary>
    /// Main entry point for the processor application
    /// Sets up infrastructure and starts the application
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Exit code (0 for success, non-zero for failure)</returns>
    public async Task<int> RunAsync(string[] args)
    {
        // Initialize console output and display startup information
        var (processorName, processorVersion) = await InitializeConsoleAndDisplayStartupInfoAsync();

        try
        {
            Console.WriteLine($"🔧 Initializing {processorName} Application...");

            _host = CreateHostBuilder(args).Build();

            // Get logger and configuration from DI container
            _logger = _host.Services.GetRequiredService<ILogger<BaseProcessorApplication>>();
            _config = _host.Services.GetRequiredService<IOptions<ProcessorConfiguration>>().Value;

            _logger.LogInformationWithCorrelation("Starting {ApplicationName}", GetType().Name);

            _logger.LogInformationWithCorrelation(
                "Initializing {ApplicationName} - {ProcessorName} v{ProcessorVersion}",
                GetType().Name, _config.Name, _config.Version);

            _logger.LogInformationWithCorrelation("Starting host services (MassTransit, Hazelcast, etc.)...");

            // Start the host first (this will start MassTransit consumers)
            await _host.StartAsync();

            _logger.LogInformationWithCorrelation("Host services started successfully. Now initializing processor...");

            // Force early initialization of metrics services to ensure meters are registered with OpenTelemetry
            // This must happen after host.StartAsync() but before processor initialization to ensure OpenTelemetry is ready
            var healthMetricsService = _host.Services.GetRequiredService<IProcessorHealthMetricsService>();
            var flowMetricsService = _host.Services.GetRequiredService<IProcessorFlowMetricsService>();

            // Allow derived classes to initialize their specific metrics services
            await InitializeCustomMetricsServicesAsync();

            _logger.LogInformationWithCorrelation("Host services initialized early to register meters with OpenTelemetry");

            // Initialize the processor service AFTER host is started
            var processorService = _host.Services.GetRequiredService<IProcessorService>();
            var initializationConfig = _host.Services.GetRequiredService<IOptions<ProcessorInitializationConfiguration>>().Value;

            if (initializationConfig.RetryEndlessly)
            {
                // Start initialization in background - don't wait for completion
                var appLifetime = _host.Services.GetRequiredService<IHostApplicationLifetime>();
                _ = Task.Run(async () =>
                {
                    // Generate correlation ID for background initialization task
                    var correlationId = Guid.NewGuid();
                    CorrelationIdContext.SetCorrelationIdStatic(correlationId);

                    try
                    {
                        await processorService.InitializeAsync(appLifetime.ApplicationStopping);
                        _logger.LogInformationWithCorrelation("Processor initialization completed successfully");
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformationWithCorrelation("Processor initialization cancelled during shutdown");
                    }
                    catch (Exception ex)
                    {
                        // Record background initialization exception
                        try
                        {
                            var healthMetricsService = _host?.Services?.GetService<IProcessorHealthMetricsService>();
                            healthMetricsService?.RecordException(ex.GetType().Name, "critical", isCritical: true);
                        }
                        catch
                        {
                            // Ignore metrics recording errors during critical failure
                        }

                        _logger.LogErrorWithCorrelation(ex, "Processor initialization failed unexpectedly");
                    }
                });

                _logger.LogInformationWithCorrelation(
                    "{ApplicationName} started successfully. Processor initialization is running in background with endless retry.",
                    GetType().Name);
            }
            else
            {
                // Legacy behavior: wait for initialization to complete
                await processorService.InitializeAsync();
                _logger.LogInformationWithCorrelation("Processor initialization completed successfully");

                _logger.LogInformationWithCorrelation(
                    "{ApplicationName} started successfully and is ready to process activities",
                    GetType().Name);
            }

            // Wait for shutdown signal
            var lifetime = _host.Services.GetRequiredService<IHostApplicationLifetime>();
            await WaitForShutdownAsync(lifetime.ApplicationStopping);

            _logger.LogInformationWithCorrelation("Shutting down {ApplicationName}", GetType().Name);

            // Stop the host gracefully
            await _host.StopAsync(TimeSpan.FromSeconds(30));

            _logger.LogInformationWithCorrelation("{ApplicationName} stopped successfully", GetType().Name);

            Console.WriteLine($"✅ {processorName} Application completed successfully");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"🛑 {processorName} Application was cancelled");
            return 0;
        }
        catch (Exception ex)
        {
            if (_logger != null)
            {
                _logger.LogCritical(ex, "Fatal error occurred in {ApplicationName}", GetType().Name);
            }

            // Record critical exception metrics
            try
            {
                var healthMetricsService = _host?.Services?.GetService<IProcessorHealthMetricsService>();
                healthMetricsService?.RecordException(ex.GetType().Name, "critical", isCritical: true);
            }
            catch
            {
                // Ignore metrics recording errors during critical failure
            }

            Console.WriteLine($"💥 {processorName} Application terminated unexpectedly: {ex.Message}");
            Console.WriteLine($"🔍 Error Context:");
            Console.WriteLine($"   • Exception Type: {ex.GetType().Name}");
            Console.WriteLine($"   • Message: {ex.Message}");
            Console.WriteLine($"   • Source: {ex.Source}");

            if (ex.InnerException != null)
            {
                Console.WriteLine($"   • Inner Exception: {ex.InnerException.Message}");
            }

            return 1;
        }
        finally
        {
            Console.WriteLine($"🧹 Shutting down {processorName} Application");
            _host?.Dispose();
        }
    }

    /// <summary>
    /// Initializes console output and displays startup information
    /// </summary>
    /// <returns>Tuple containing processor name and version</returns>
    private async Task<(string processorName, string processorVersion)> InitializeConsoleAndDisplayStartupInfoAsync()
    {
        // Force console output to be visible - bypass any logging framework interference
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.WriteLine("=== PROCESSOR STARTING ===");
        Console.WriteLine($"Current Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        // Load early configuration to get processor info
        var configuration = LoadEarlyConfiguration();
        var processorName = configuration["ProcessorConfiguration:Name"] ?? "Unknown";
        var processorVersion = configuration["ProcessorConfiguration:Version"] ?? "Unknown";

        // Display application information
        DisplayApplicationInformation(processorName, processorVersion);

        // Perform environment validation
        await ValidateEnvironmentAsync();

        // Display configuration
        await DisplayConfigurationAsync();

        return (processorName, processorVersion);
    }

    /// <summary>
    /// Loads early configuration before host is built
    /// </summary>
    /// <returns>Configuration instance</returns>
    private IConfiguration LoadEarlyConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    /// <summary>
    /// Displays application information
    /// </summary>
    /// <param name="processorName">Name of the processor</param>
    /// <param name="processorVersion">Version of the processor</param>
    private void DisplayApplicationInformation(string processorName, string processorVersion)
    {
        Console.WriteLine($"🌟 Starting {processorName} v{processorVersion}");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("📋 Application Information:");
        Console.WriteLine($"   🔖 Config Version: {processorVersion}");

        // Try to get implementation hash if available
        try
        {
            var hashType = Type.GetType("ProcessorImplementationHash");
            if (hashType != null)
            {
                var versionProp = hashType.GetProperty("Version");
                var hashProp = hashType.GetProperty("Hash");
                var sourceFileProp = hashType.GetProperty("SourceFile");
                var generatedAtProp = hashType.GetProperty("GeneratedAt");

                Console.WriteLine($"   📦 Assembly Version: {versionProp?.GetValue(null) ?? "Unknown"}");
                Console.WriteLine($"   🔐 SHA Hash: {hashProp?.GetValue(null) ?? "Unknown"}");
                Console.WriteLine($"   📝 Source File: {sourceFileProp?.GetValue(null) ?? "Unknown"}");
                Console.WriteLine($"   🕒 Hash Generated: {generatedAtProp?.GetValue(null) ?? "Unknown"}");
            }
        }
        catch
        {
            Console.WriteLine($"   📦 Assembly Version: Unknown");
            Console.WriteLine($"   🔐 SHA Hash: Unknown");
        }

        Console.WriteLine($"   🏷️  Processor Name: {processorName}");
        Console.WriteLine($"   🌍 Environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}");
        Console.WriteLine($"   🖥️  Machine: {Environment.MachineName}");
        Console.WriteLine($"   👤 User: {Environment.UserName}");
        Console.WriteLine($"   📁 Working Directory: {Environment.CurrentDirectory}");
        Console.WriteLine($"   🆔 Process ID: {Environment.ProcessId}");
        Console.WriteLine($"   ⚙️  .NET Version: {Environment.Version}");
        Console.WriteLine($"   🕒 Started At: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
        Console.WriteLine("═══════════════════════════════════════════════════════");

        // Allow derived classes to add custom application info
        DisplayCustomApplicationInfo();
    }

    /// <summary>
    /// Virtual method that derived classes can override to display custom application information
    /// </summary>
    protected virtual void DisplayCustomApplicationInfo()
    {
        // Default implementation does nothing
        // Derived classes can override to add custom information
    }

    /// <summary>
    /// Performs environment validation
    /// </summary>
    protected virtual async Task ValidateEnvironmentAsync()
    {
        Console.WriteLine("🔍 Performing Environment Validation...");

        var validationResults = new List<(string Check, bool Passed, string Message)>();

        // Check required environment variables
        var requiredEnvVars = new[] { "ASPNETCORE_ENVIRONMENT" };
        foreach (var envVar in requiredEnvVars)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            var passed = !string.IsNullOrEmpty(value);
            validationResults.Add((envVar, passed, passed ? $"✅ {envVar}={value}" : $"⚠️  {envVar} not set"));
        }

        // Check system resources
        var memoryMB = Environment.WorkingSet / 1024.0 / 1024.0;
        var memoryOk = memoryMB < 1000; // Less than 1GB
        validationResults.Add(("Memory", memoryOk,
            memoryOk ? $"✅ Memory usage: {memoryMB:F1} MB" : $"⚠️  High memory usage: {memoryMB:F1} MB"));

        // Check processor manager connectivity
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync("http://localhost:5110/health");
            var passed = response.IsSuccessStatusCode;
            validationResults.Add(("ProcessorManager", passed,
                passed ? "✅ Processor Manager connectivity verified" : $"⚠️  Processor Manager returned: {response.StatusCode}"));
        }
        catch (Exception ex)
        {
            validationResults.Add(("ProcessorManager", false, $"⚠️  Processor Manager unreachable: {ex.Message}"));
        }

        // Check schema manager connectivity
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync("http://localhost:5100/health");
            var passed = response.IsSuccessStatusCode;
            validationResults.Add(("SchemaManager", passed,
                passed ? "✅ Schema Manager connectivity verified" : $"⚠️  Schema Manager returned: {response.StatusCode}"));
        }
        catch (Exception ex)
        {
            validationResults.Add(("SchemaManager", false, $"⚠️  Schema Manager unreachable: {ex.Message}"));
        }

        // Allow derived classes to add custom validations
        await PerformCustomEnvironmentValidationAsync(validationResults);

        // Log validation results
        Console.WriteLine("📊 Environment Validation Results:");
        foreach (var (check, passed, message) in validationResults)
        {
            Console.WriteLine($"   {message}");
        }

        var passedCount = validationResults.Count(r => r.Passed);
        var totalCount = validationResults.Count;

        if (passedCount == totalCount)
        {
            Console.WriteLine($"🎉 All environment validations passed ({passedCount}/{totalCount})");
        }
        else
        {
            Console.WriteLine($"⚠️  Environment validation completed with warnings ({passedCount}/{totalCount} passed)");
            Console.WriteLine("💡 Warnings are normal if dependent services are not running yet");
        }

        Console.WriteLine("✅ Environment validation completed");
    }

    /// <summary>
    /// Virtual method that derived classes can override to add custom environment validations
    /// </summary>
    /// <param name="validationResults">List to add validation results to</param>
    protected virtual async Task PerformCustomEnvironmentValidationAsync(List<(string Check, bool Passed, string Message)> validationResults)
    {
        // Default implementation does nothing
        // Derived classes can override to add custom validations
        await Task.CompletedTask;
    }

    /// <summary>
    /// Displays configuration information
    /// </summary>
    protected virtual async Task DisplayConfigurationAsync()
    {
        Console.WriteLine("📋 Loading and Displaying Configuration...");

        try
        {
            var configuration = LoadEarlyConfiguration();

            // Read and display the entire appsettings.json content
            var appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            if (File.Exists(appSettingsPath))
            {
                var appSettingsContent = await File.ReadAllTextAsync(appSettingsPath);
                var formattedJson = JsonSerializer.Serialize(
                    JsonSerializer.Deserialize<object>(appSettingsContent),
                    new JsonSerializerOptions { WriteIndented = true });

                Console.WriteLine("📄 Configuration Content:");
                Console.WriteLine(formattedJson);
            }

            Console.WriteLine("✅ Configuration display completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error displaying configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates and configures the host builder with all necessary services
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Configured host builder</returns>
    protected virtual IHostBuilder CreateHostBuilder(string[] args)
    {
        // Find the project directory by looking for the .csproj file
        var currentDir = Directory.GetCurrentDirectory();
        var projectDir = FindProjectDirectory(currentDir);

        return Host.CreateDefaultBuilder(args)
            .UseContentRoot(projectDir)
            .UseEnvironment(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
            .ConfigureLogging(logging =>
            {
                // Clear default logging providers - OpenTelemetry will handle logging
                logging.ClearProviders();

                // Allow derived classes to add additional logging providers before OpenTelemetry setup
                ConfigureLogging(logging);
            })
            .ConfigureServices((context, services) =>
            {
                // Configure application settings
                services.Configure<ProcessorConfiguration>(
                    context.Configuration.GetSection("ProcessorConfiguration"));
                services.Configure<ProcessorHazelcastConfiguration>(
                    context.Configuration.GetSection("Hazelcast"));
                services.Configure<SchemaValidationConfiguration>(
                    context.Configuration.GetSection("SchemaValidation"));
                services.Configure<ProcessorInitializationConfiguration>(
                    context.Configuration.GetSection("ProcessorInitialization"));
                services.Configure<ProcessorHealthMonitorConfiguration>(
                    context.Configuration.GetSection("ProcessorHealthMonitor"));

                // Add core services
                services.AddSingleton<IActivityExecutor>(this);
                services.AddSingleton<IProcessorService, ProcessorService>();
                services.AddSingleton<ISchemaValidator, SchemaValidator>();

                // Add metrics labeling service (must be registered before other metrics services)
                services.AddSingleton<IProcessorMetricsLabelService, ProcessorMetricsLabelService>();

                // Add health monitoring services
                services.AddSingleton<IPerformanceMetricsService, PerformanceMetricsService>();
                services.AddSingleton<IProcessorHealthMetricsService, ProcessorHealthMetricsService>();
                services.AddSingleton<IProcessorHealthMonitor, ProcessorHealthMonitor>();
                services.AddHostedService<ProcessorHealthMonitor>();

                // Add flow metrics service (optimized for anomaly detection)
                services.AddSingleton<IProcessorFlowMetricsService, ProcessorFlowMetricsService>();
                
                // Add infrastructure services
                services.AddMassTransitWithRabbitMq(context.Configuration,
                    typeof(ExecuteActivityCommandConsumer));
                services.AddHazelcastClient(context.Configuration);

                // Add OpenTelemetry - use ProcessorConfiguration values for service name/version
                var processorName = context.Configuration["ProcessorConfiguration:Name"];
                var processorVersion = context.Configuration["ProcessorConfiguration:Version"];
                services.AddOpenTelemetryObservability(context.Configuration, processorName, processorVersion);

                // Allow derived classes to add custom services
                ConfigureServices(services, context.Configuration);
            });
    }

    /// <summary>
    /// Virtual method that derived classes can override to add custom services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    protected virtual void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Default implementation does nothing
        // Derived classes can override to add custom services
    }

    /// <summary>
    /// Virtual method that derived classes can override to configure logging
    /// This is called before OpenTelemetry logging is configured
    /// </summary>
    /// <param name="logging">Logging builder</param>
    protected virtual void ConfigureLogging(ILoggingBuilder logging)
    {
        // Default implementation does nothing
        // Derived classes can override to add custom logging providers
    }

    /// <summary>
    /// Virtual method that derived classes can override to initialize custom metrics services
    /// This is called after host.StartAsync() but before processor initialization
    /// </summary>
    protected virtual async Task InitializeCustomMetricsServicesAsync()
    {
        // Default implementation does nothing
        // Derived classes can override to initialize their specific metrics services
        await Task.CompletedTask;
    }

    /// <summary>
    /// Finds the project directory by looking for the .csproj file
    /// </summary>
    /// <param name="startDirectory">Directory to start searching from</param>
    /// <returns>Project directory path</returns>
    private static string FindProjectDirectory(string startDirectory)
    {
        var currentDir = new DirectoryInfo(startDirectory);

        // Look for .csproj file in current directory and parent directories
        while (currentDir != null)
        {
            var csprojFiles = currentDir.GetFiles("*.csproj");
            if (csprojFiles.Length > 0)
            {
                return currentDir.FullName;
            }

            // Check if we're in the BaseProcessor.Application directory specifically
            if (currentDir.Name == "FlowOrchestrator.BaseProcessor.Application")
            {
                return currentDir.FullName;
            }

            currentDir = currentDir.Parent;
        }

        // Fallback: try to find the BaseProcessor.Application directory
        var baseDir = startDirectory;
        var targetPath = Path.Combine(baseDir, "src", "Framework", "FlowOrchestrator.BaseProcessor.Application");
        if (Directory.Exists(targetPath))
        {
            return targetPath;
        }

        // Final fallback: use current directory
        return startDirectory;
    }

    /// <summary>
    /// Waits for shutdown signal
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the wait operation</returns>
    private static async Task WaitForShutdownAsync(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();

        cancellationToken.Register(() => tcs.SetResult(true));

        // Also listen for Ctrl+C
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            tcs.SetResult(true);
        };

        await tcs.Task;
    }
}


