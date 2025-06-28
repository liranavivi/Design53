using Manager.OrchestratedFlow.Consumers;
using Manager.OrchestratedFlow.Repositories;
using Manager.OrchestratedFlow.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shared.Configuration;
using Shared.Correlation;
using Shared.HealthChecks;
using Shared.Models;
using Shared.Services;

var builder = WebApplication.CreateBuilder(args);

// Clear default logging providers - OpenTelemetry will handle logging
builder.Logging.ClearProviders();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Manager Configuration
builder.Services.Configure<ManagerConfiguration>(builder.Configuration.GetSection("ManagerConfiguration"));

// Add Correlation ID services
builder.Services.AddCorrelationId();

// Add Manager Metrics Service
builder.Services.AddSingleton<IManagerMetricsService, ManagerMetricsService>();

// Add MongoDB
builder.Services.AddMongoDb<IOrchestratedFlowEntityRepository, OrchestratedFlowEntityRepository>(builder.Configuration, builder.Configuration["MongoDB:DatabaseName"]!);

// Add HTTP clients for validation services with correlation ID support
builder.Services.AddHttpClient<IWorkflowValidationService, WorkflowValidationService>(client =>
{
    var workflowManagerUrl = builder.Configuration["Services:WorkflowManager:BaseUrl"] ?? "http://localhost:60890";
    client.BaseAddress = new Uri(workflowManagerUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

builder.Services.AddHttpClient<IAssignmentValidationService, AssignmentValidationService>(client =>
{
    var assignmentManagerUrl = builder.Configuration["Services:AssignmentManager:BaseUrl"] ?? "http://localhost:60888";
    client.BaseAddress = new Uri(assignmentManagerUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

// Add MassTransit with RabbitMQ
builder.Services.AddMassTransitWithRabbitMq(builder.Configuration,
    typeof(CreateOrchestratedFlowCommandConsumer),
    typeof(UpdateOrchestratedFlowCommandConsumer),
    typeof(DeleteOrchestratedFlowCommandConsumer),
    typeof(GetOrchestratedFlowQueryConsumer));

// Add OpenTelemetry
var serviceName = builder.Configuration["OpenTelemetry:ServiceName"];
var serviceVersion = builder.Configuration["OpenTelemetry:ServiceVersion"];
builder.Services.AddOpenTelemetryObservability(builder.Configuration, serviceName, serviceVersion);

// Add Health Checks
builder.Services.AddHttpClient<OpenTelemetryHealthCheck>();
builder.Services.AddHealthChecks()
    .AddMongoDb(builder.Configuration.GetConnectionString("MongoDB")!)
    .AddRabbitMQ(rabbitConnectionString: $"amqp://{builder.Configuration["RabbitMQ:Username"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:Host"]}:{5672}/")
    .AddCheck<OpenTelemetryHealthCheck>("opentelemetry");

var app = builder.Build();

// Force early initialization of ManagerMetricsService to ensure meter is registered with OpenTelemetry
// This must happen after app.Build() but before app.Run() to ensure OpenTelemetry is ready
var metricsService = app.Services.GetRequiredService<IManagerMetricsService>();
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("ManagerMetricsService initialized early to register meters with OpenTelemetry");

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
app.UseSwagger();
app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Add correlation ID middleware (must be early in pipeline)
app.UseCorrelationId();

app.UseRouting();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var metricsService = context.RequestServices.GetRequiredService<IManagerMetricsService>();
        var status = report.Status switch
        {
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy => 0,
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded => 1,
            _ => 2
        };
        metricsService.RecordHealthStatus(status);

        // Write simple JSON response
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { status = report.Status.ToString() }));
    }
});

try
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Starting OrchestratedFlowManager API");
    app.Run();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogCritical(ex, "Application terminated unexpectedly");
}

// Make Program class accessible for testing
public partial class Program { }
