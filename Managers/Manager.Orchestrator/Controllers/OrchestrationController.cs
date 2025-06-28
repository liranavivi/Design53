using Manager.Orchestrator.Models;
using Manager.Orchestrator.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Correlation;
using Swashbuckle.AspNetCore.Annotations;

namespace Manager.Orchestrator.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Orchestration management operations")]
public class OrchestrationController : ControllerBase
{
    private readonly IOrchestrationService _orchestrationService;
    private readonly IOrchestrationSchedulerService _schedulerService;
    private readonly ILogger<OrchestrationController> _logger;

    public OrchestrationController(
        IOrchestrationService orchestrationService,
        IOrchestrationSchedulerService schedulerService,
        ILogger<OrchestrationController> logger)
    {
        _orchestrationService = orchestrationService;
        _schedulerService = schedulerService;
        _logger = logger;
    }

    /// <summary>
    /// Starts orchestration for the specified orchestrated flow ID
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID to start</param>
    /// <returns>Success response</returns>
    [HttpPost("start/{orchestratedFlowId}")]
    [SwaggerOperation(Summary = "Start orchestration", Description = "Starts orchestration by gathering all required data from managers and storing in cache")]
    [SwaggerResponse(200, "Orchestration started successfully")]
    [SwaggerResponse(400, "Invalid orchestrated flow ID")]
    [SwaggerResponse(404, "Orchestrated flow not found")]
    [SwaggerResponse(500, "Internal server error")]
    public async Task<ActionResult> Start(string orchestratedFlowId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(orchestratedFlowId, out Guid guidOrchestratedFlowId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided for Start orchestration. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                orchestratedFlowId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {orchestratedFlowId}");
        }

        _logger.LogInformationWithCorrelation("Starting Start orchestration request. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
            guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);

        try
        {
            await _orchestrationService.StartOrchestrationAsync(guidOrchestratedFlowId);

            _logger.LogInformationWithCorrelation("Successfully started orchestration. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);

            return Ok(new { 
                message = "Orchestration started successfully", 
                orchestratedFlowId = guidOrchestratedFlowId,
                startedAt = DateTime.UtcNow
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Invalid operation during Start orchestration. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error during Start orchestration. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while starting orchestration");
        }
    }

    /// <summary>
    /// Stops orchestration for the specified orchestrated flow ID
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID to stop</param>
    /// <returns>Success response</returns>
    [HttpPost("stop/{orchestratedFlowId}")]
    [SwaggerOperation(Summary = "Stop orchestration", Description = "Stops orchestration by removing data from cache and performing cleanup")]
    [SwaggerResponse(200, "Orchestration stopped successfully")]
    [SwaggerResponse(400, "Invalid orchestrated flow ID")]
    [SwaggerResponse(500, "Internal server error")]
    public async Task<ActionResult> Stop(string orchestratedFlowId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(orchestratedFlowId, out Guid guidOrchestratedFlowId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided for Stop orchestration. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                orchestratedFlowId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {orchestratedFlowId}");
        }

        _logger.LogInformationWithCorrelation("Starting Stop orchestration request. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
            guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);

        try
        {
            await _orchestrationService.StopOrchestrationAsync(guidOrchestratedFlowId);

            _logger.LogInformationWithCorrelation("Successfully stopped orchestration. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);

            return Ok(new { 
                message = "Orchestration stopped successfully", 
                orchestratedFlowId = guidOrchestratedFlowId,
                stoppedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error during Stop orchestration. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while stopping orchestration");
        }
    }

    /// <summary>
    /// Gets orchestration status for the specified orchestrated flow ID
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID to check</param>
    /// <returns>Orchestration status information</returns>
    [HttpGet("status/{orchestratedFlowId}")]
    [SwaggerOperation(Summary = "Get orchestration status", Description = "Gets the current status of orchestration including active state and metadata")]
    [SwaggerResponse(200, "Orchestration status retrieved successfully")]
    [SwaggerResponse(400, "Invalid orchestrated flow ID")]
    [SwaggerResponse(500, "Internal server error")]
    public async Task<ActionResult<OrchestrationStatusModel>> GetStatus(string orchestratedFlowId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(orchestratedFlowId, out Guid guidOrchestratedFlowId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided for Get orchestration status. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                orchestratedFlowId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {orchestratedFlowId}");
        }

        _logger.LogInformationWithCorrelation("Starting Get orchestration status request. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
            guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var status = await _orchestrationService.GetOrchestrationStatusAsync(guidOrchestratedFlowId);

            _logger.LogInformationWithCorrelation("Successfully retrieved orchestration status. OrchestratedFlowId: {OrchestratedFlowId}, IsActive: {IsActive}, StepCount: {StepCount}, AssignmentCount: {AssignmentCount}, User: {User}, RequestId: {RequestId}",
                guidOrchestratedFlowId, status.IsActive, status.StepCount, status.AssignmentCount, userContext, HttpContext.TraceIdentifier);

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error during Get orchestration status. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving orchestration status");
        }
    }

    /// <summary>
    /// Gets health status for a specific processor
    /// </summary>
    /// <param name="processorId">The processor ID to check</param>
    /// <returns>Processor health status</returns>
    [HttpGet("processor-health/{processorId}")]
    [SwaggerOperation(Summary = "Get processor health", Description = "Gets the health status of a specific processor from cache")]
    [SwaggerResponse(200, "Processor health retrieved successfully", typeof(Shared.Models.ProcessorHealthResponse))]
    [SwaggerResponse(400, "Invalid processor ID")]
    [SwaggerResponse(404, "Processor health data not found")]
    [SwaggerResponse(500, "Internal server error")]
    public async Task<ActionResult<Shared.Models.ProcessorHealthResponse>> GetProcessorHealth(string processorId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(processorId, out Guid guidProcessorId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided for Get processor health. ProcessorId: {ProcessorId}, User: {User}, RequestId: {RequestId}",
                processorId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {processorId}");
        }

        _logger.LogInformationWithCorrelation("Starting Get processor health request. ProcessorId: {ProcessorId}, User: {User}, RequestId: {RequestId}",
            guidProcessorId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var healthResponse = await _orchestrationService.GetProcessorHealthAsync(guidProcessorId);

            if (healthResponse == null)
            {
                _logger.LogInformationWithCorrelation("Processor health data not found. ProcessorId: {ProcessorId}, User: {User}, RequestId: {RequestId}",
                    guidProcessorId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Processor health data not found for processor ID: {guidProcessorId}");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved processor health. ProcessorId: {ProcessorId}, Status: {Status}, User: {User}, RequestId: {RequestId}",
                guidProcessorId, healthResponse.Status, userContext, HttpContext.TraceIdentifier);

            return Ok(healthResponse);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error during Get processor health. ProcessorId: {ProcessorId}, User: {User}, RequestId: {RequestId}",
                guidProcessorId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving processor health");
        }
    }

    /// <summary>
    /// Gets health status for all processors in a specific orchestrated flow
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID to check</param>
    /// <returns>Health status of all processors in the orchestrated flow</returns>
    [HttpGet("processors-health/{orchestratedFlowId}")]
    [SwaggerOperation(Summary = "Get processors health by orchestrated flow", Description = "Gets the health status of all processors in a specific orchestrated flow (only available if orchestrated flow exists in cache)")]
    [SwaggerResponse(200, "Processors health retrieved successfully", typeof(Shared.Models.ProcessorsHealthResponse))]
    [SwaggerResponse(400, "Invalid orchestrated flow ID")]
    [SwaggerResponse(404, "Orchestrated flow not found in cache")]
    [SwaggerResponse(500, "Internal server error")]
    public async Task<ActionResult<Shared.Models.ProcessorsHealthResponse>> GetProcessorsHealthByOrchestratedFlow(string orchestratedFlowId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(orchestratedFlowId, out Guid guidOrchestratedFlowId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided for Get processors health by orchestrated flow. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                orchestratedFlowId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {orchestratedFlowId}");
        }

        _logger.LogInformationWithCorrelation("Starting Get processors health by orchestrated flow request. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
            guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var healthResponse = await _orchestrationService.GetProcessorsHealthByOrchestratedFlowAsync(guidOrchestratedFlowId);

            if (healthResponse == null)
            {
                _logger.LogInformationWithCorrelation("Orchestrated flow not found in cache. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                    guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Orchestrated flow not found in cache: {guidOrchestratedFlowId}");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved processors health for orchestrated flow. OrchestratedFlowId: {OrchestratedFlowId}, " +
                                 "TotalProcessors: {TotalProcessors}, HealthyProcessors: {HealthyProcessors}, OverallStatus: {OverallStatus}, User: {User}, RequestId: {RequestId}",
                guidOrchestratedFlowId, healthResponse.Summary.TotalProcessors, healthResponse.Summary.HealthyProcessors,
                healthResponse.Summary.OverallStatus, userContext, HttpContext.TraceIdentifier);

            return Ok(healthResponse);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error during Get processors health by orchestrated flow. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving processors health");
        }
    }

    /// <summary>
    /// Starts the scheduler for the specified orchestrated flow
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID to schedule</param>
    /// <param name="request">Request containing cron expression</param>
    /// <returns>Success response</returns>
    [HttpPost("scheduler/start/{orchestratedFlowId}")]
    [SwaggerOperation(Summary = "Start scheduler", Description = "Starts scheduled execution for an orchestrated flow using a cron expression")]
    [SwaggerResponse(200, "Scheduler started successfully")]
    [SwaggerResponse(400, "Invalid orchestrated flow ID or cron expression")]
    [SwaggerResponse(409, "Scheduler already running for this flow")]
    [SwaggerResponse(500, "Internal server error")]
    public async Task<ActionResult> StartScheduler(string orchestratedFlowId, [FromBody] StartSchedulerRequest request)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(orchestratedFlowId, out Guid guidOrchestratedFlowId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided for Start scheduler. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                orchestratedFlowId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {orchestratedFlowId}");
        }

        if (string.IsNullOrWhiteSpace(request.CronExpression))
        {
            return BadRequest("Cron expression is required");
        }

        _logger.LogInformationWithCorrelation("Starting scheduler for orchestrated flow. OrchestratedFlowId: {OrchestratedFlowId}, CronExpression: {CronExpression}, User: {User}, RequestId: {RequestId}",
            guidOrchestratedFlowId, request.CronExpression, userContext, HttpContext.TraceIdentifier);

        try
        {
            await _schedulerService.StartSchedulerAsync(guidOrchestratedFlowId, request.CronExpression);

            var nextExecution = await _schedulerService.GetNextExecutionTimeAsync(guidOrchestratedFlowId);

            _logger.LogInformationWithCorrelation("Successfully started scheduler for orchestrated flow. OrchestratedFlowId: {OrchestratedFlowId}, NextExecution: {NextExecution}, User: {User}, RequestId: {RequestId}",
                guidOrchestratedFlowId, nextExecution, userContext, HttpContext.TraceIdentifier);

            return Ok(new {
                message = "Scheduler started successfully",
                orchestratedFlowId = guidOrchestratedFlowId,
                cronExpression = request.CronExpression,
                nextExecution = nextExecution,
                startedAt = DateTime.UtcNow
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Invalid cron expression for Start scheduler. OrchestratedFlowId: {OrchestratedFlowId}, CronExpression: {CronExpression}, User: {User}, RequestId: {RequestId}",
                guidOrchestratedFlowId, request.CronExpression, userContext, HttpContext.TraceIdentifier);
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Scheduler already running for Start scheduler. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);
            return Conflict(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error during Start scheduler. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while starting scheduler");
        }
    }

    /// <summary>
    /// Stops the scheduler for the specified orchestrated flow
    /// </summary>
    /// <param name="orchestratedFlowId">The orchestrated flow ID to stop scheduling</param>
    /// <returns>Success response</returns>
    [HttpPost("scheduler/stop/{orchestratedFlowId}")]
    [SwaggerOperation(Summary = "Stop scheduler", Description = "Stops scheduled execution for an orchestrated flow")]
    [SwaggerResponse(200, "Scheduler stopped successfully")]
    [SwaggerResponse(400, "Invalid orchestrated flow ID")]
    [SwaggerResponse(404, "No scheduler running for this flow")]
    [SwaggerResponse(500, "Internal server error")]
    public async Task<ActionResult> StopScheduler(string orchestratedFlowId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(orchestratedFlowId, out Guid guidOrchestratedFlowId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided for Stop scheduler. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                orchestratedFlowId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {orchestratedFlowId}");
        }

        _logger.LogInformationWithCorrelation("Stopping scheduler for orchestrated flow. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
            guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);

        try
        {
            await _schedulerService.StopSchedulerAsync(guidOrchestratedFlowId);

            _logger.LogInformationWithCorrelation("Successfully stopped scheduler for orchestrated flow. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);

            return Ok(new {
                message = "Scheduler stopped successfully",
                orchestratedFlowId = guidOrchestratedFlowId,
                stoppedAt = DateTime.UtcNow
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "No scheduler running for Stop scheduler. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error during Stop scheduler. OrchestratedFlowId: {OrchestratedFlowId}, User: {User}, RequestId: {RequestId}",
                guidOrchestratedFlowId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while stopping scheduler");
        }
    }
}
