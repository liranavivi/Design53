using Manager.Workflow.Repositories;
using Manager.Workflow.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Entities;
using Shared.Exceptions;
using Shared.Correlation;
using Swashbuckle.AspNetCore.Annotations;

namespace Manager.Workflow.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Workflow management operations")]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowEntityRepository _repository;
    private readonly IOrchestratedFlowValidationService _orchestratedFlowValidationService;
    private readonly IWorkflowValidationService _workflowValidator;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(
        IWorkflowEntityRepository repository,
        IOrchestratedFlowValidationService orchestratedFlowValidationService,
        IWorkflowValidationService workflowValidator,
        ILogger<WorkflowController> logger)
    {
        _repository = repository;
        _orchestratedFlowValidationService = orchestratedFlowValidationService;
        _workflowValidator = workflowValidator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkflowEntity>>> GetAll()
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetAll schemas request. User: {User}, RequestId: {RequestId}",
            userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetAllAsync();
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Workflow entities. User: {User}, RequestId: {RequestId}",
                count, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving all Workflow entities. User: {User}, RequestId: {RequestId}",
                userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Workflow entities");
        }
    }

    [HttpGet("paged")]
    public async Task<ActionResult<object>> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        var originalPage = page;
        var originalPageSize = pageSize;

        _logger.LogInformationWithCorrelation("Starting GetPaged schemas request. Page: {Page}, PageSize: {PageSize}, User: {User}, RequestId: {RequestId}",
            page, pageSize, userContext, HttpContext.TraceIdentifier);

        try
        {
            // Strict validation - return 400 for invalid parameters instead of auto-correcting
            if (page < 1)
            {
                _logger.LogWarningWithCorrelation("Invalid page parameter {Page} provided. User: {User}, RequestId: {RequestId}",
                    originalPage, userContext, HttpContext.TraceIdentifier);
                return BadRequest(new
                {
                    error = "Invalid page parameter",
                    message = "Page must be greater than 0",
                    parameter = "page",
                    value = originalPage
                });
            }

            if (pageSize < 1)
            {
                _logger.LogWarningWithCorrelation("Invalid pageSize parameter {PageSize} provided. User: {User}, RequestId: {RequestId}",
                    originalPageSize, userContext, HttpContext.TraceIdentifier);
                return BadRequest(new
                {
                    error = "Invalid pageSize parameter",
                    message = "PageSize must be greater than 0",
                    parameter = "pageSize",
                    value = originalPageSize
                });
            }
            else if (pageSize > 100)
            {
                _logger.LogWarningWithCorrelation("PageSize parameter {PageSize} exceeds maximum. User: {User}, RequestId: {RequestId}",
                    originalPageSize, userContext, HttpContext.TraceIdentifier);
                return BadRequest(new
                {
                    error = "Invalid pageSize parameter",
                    message = "PageSize cannot exceed 100",
                    parameter = "pageSize",
                    value = originalPageSize,
                    maximum = 100
                });
            }

            var entities = await _repository.GetPagedAsync(page, pageSize);
            var totalCount = await _repository.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var result = new
            {
                data = entities,
                page = page,
                pageSize = pageSize,
                totalCount = totalCount,
                totalPages = totalPages
            };

            _logger.LogInformationWithCorrelation("Successfully retrieved paged Workflow entities. Page: {Page}/{TotalPages}, PageSize: {PageSize}, TotalCount: {TotalCount}, User: {User}, RequestId: {RequestId}",
                page, totalPages, pageSize, totalCount, userContext, HttpContext.TraceIdentifier);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving paged Workflow entities. Page: {Page}, PageSize: {PageSize}, User: {User}, RequestId: {RequestId}",
                originalPage, originalPageSize, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving paged Workflow entities");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkflowEntity>> GetById(string id)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(id, out Guid guidId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided. Id: {Id}, User: {User}, RequestId: {RequestId}",
                id, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {id}");
        }

        _logger.LogInformationWithCorrelation("Starting GetById Workflow request. Id: {Id}, User: {User}, RequestId: {RequestId}",
            guidId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entity = await _repository.GetByIdAsync(guidId);

            if (entity == null)
            {
                _logger.LogWarningWithCorrelation("Workflow entity not found. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Workflow entity with ID {guidId} not found");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved Workflow entity. Id: {Id}, Version: {Version}, Name: {Name}, StepIds: {StepIds}, User: {User}, RequestId: {RequestId}",
                entity.Id, entity.Version, entity.Name, string.Join(",", entity.StepIds), userContext, HttpContext.TraceIdentifier);

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Workflow entity by ID. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving the Workflow entity");
        }
    }

    [HttpGet("composite/{version}/{name}")]
    public async Task<ActionResult<WorkflowEntity>> GetByCompositeKey(string version, string name)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        // WorkflowEntity composite key format: "version_name"
        var compositeKey = $"{version}_{name}";

        _logger.LogInformationWithCorrelation("Starting GetByCompositeKey Workflow request. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            version, name, compositeKey, userContext, HttpContext.TraceIdentifier);

        try
        {
            if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarningWithCorrelation("Empty or null version/name provided. Version: {Version}, Name: {Name}, User: {User}, RequestId: {RequestId}",
                    version, name, userContext, HttpContext.TraceIdentifier);
                return BadRequest("Version and name cannot be empty");
            }

            var entity = await _repository.GetByCompositeKeyAsync(compositeKey);

            if (entity == null)
            {
                _logger.LogWarningWithCorrelation("Workflow entity not found by composite key. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                    version, name, compositeKey, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Workflow entity with version '{version}' and name '{name}' not found");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved Workflow entity by composite key. CompositeKey: {CompositeKey}, Id: {Id}, Version: {Version}, Name: {Name}, StepIds: {StepIds}, User: {User}, RequestId: {RequestId}",
                compositeKey, entity.Id, entity.Version, entity.Name, string.Join(",", entity.StepIds), userContext, HttpContext.TraceIdentifier);

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Workflow entity by composite key. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                version, name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving the Workflow entity");
        }
    }

    [HttpGet("step/{stepId}")]
    public async Task<ActionResult<IEnumerable<WorkflowEntity>>> GetByStepId(string stepId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(stepId, out Guid guidStepId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided for GetByStepId. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
                stepId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {stepId}");
        }

        _logger.LogInformationWithCorrelation("Starting GetByStepId Workflow request. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
            guidStepId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetByStepIdAsync(guidStepId);
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Workflow entities by stepId. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
                count, guidStepId, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Workflow entities by stepId. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
                guidStepId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Workflow entities");
        }
    }

    [HttpGet("step/{stepId}/exists")]
    public async Task<ActionResult<bool>> CheckStepReferences(string stepId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(stepId, out Guid guidStepId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided for CheckStepReferences. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
                stepId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {stepId}");
        }

        _logger.LogInformationWithCorrelation("Starting CheckStepReferences Workflow request. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
            guidStepId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetByStepIdAsync(guidStepId);
            var hasReferences = entities.Any();

            _logger.LogInformationWithCorrelation("Successfully checked step references. StepId: {StepId}, HasReferences: {HasReferences}, User: {User}, RequestId: {RequestId}",
                guidStepId, hasReferences, userContext, HttpContext.TraceIdentifier);

            return Ok(hasReferences);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error checking step references. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
                guidStepId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while checking step references");
        }
    }

    [HttpGet("version/{version}")]
    public async Task<ActionResult<IEnumerable<WorkflowEntity>>> GetByVersion(string version)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByVersion Workflow request. Version: {Version}, User: {User}, RequestId: {RequestId}",
            version, userContext, HttpContext.TraceIdentifier);

        try
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                _logger.LogWarningWithCorrelation("Empty or null version provided. User: {User}, RequestId: {RequestId}",
                    userContext, HttpContext.TraceIdentifier);
                return BadRequest("Version cannot be empty");
            }

            var entities = await _repository.GetByVersionAsync(version);
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Workflow entities by version. Version: {Version}, User: {User}, RequestId: {RequestId}",
                count, version, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Workflow entities by version. Version: {Version}, User: {User}, RequestId: {RequestId}",
                version, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Workflow entities");
        }
    }

    [HttpGet("name/{name}")]
    public async Task<ActionResult<IEnumerable<WorkflowEntity>>> GetByName(string name)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByName Workflow request. Name: {Name}, User: {User}, RequestId: {RequestId}",
            name, userContext, HttpContext.TraceIdentifier);

        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarningWithCorrelation("Empty or null name provided. User: {User}, RequestId: {RequestId}",
                    userContext, HttpContext.TraceIdentifier);
                return BadRequest("Name cannot be empty");
            }

            var entities = await _repository.GetByNameAsync(name);
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Workflow entities by name. Name: {Name}, User: {User}, RequestId: {RequestId}",
                count, name, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Workflow entities by name. Name: {Name}, User: {User}, RequestId: {RequestId}",
                name, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Workflow entities");
        }
    }

    [HttpPost]
    public async Task<ActionResult<WorkflowEntity>> Create([FromBody] WorkflowEntity entity)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        var compositeKey = entity?.GetCompositeKey() ?? "Unknown";

        _logger.LogInformationWithCorrelation("Starting Create Workflow request. CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            compositeKey, userContext, HttpContext.TraceIdentifier);

        if (entity == null)
        {
            _logger.LogWarningWithCorrelation("Null entity provided for Workflow creation. User: {User}, RequestId: {RequestId}",
                userContext, HttpContext.TraceIdentifier);
            return BadRequest("Workflow entity cannot be null");
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarningWithCorrelation("Invalid model state for Workflow creation. Errors: {Errors}, User: {User}, RequestId: {RequestId}",
                errorMessage, userContext, HttpContext.TraceIdentifier);
            return BadRequest(ModelState);
        }

        try
        {
            entity!.CreatedBy = userContext;
            entity.Id = Guid.Empty;

            // Validate step IDs exist before creating the entity
            _logger.LogDebugWithCorrelation("Validating step references before creating Workflow entity. StepIds: {StepIds}, User: {User}, RequestId: {RequestId}",
                string.Join(",", entity.StepIds), userContext, HttpContext.TraceIdentifier);

            await _workflowValidator.ValidateStepsExist(entity.StepIds);

            _logger.LogDebugWithCorrelation("All validations passed. Creating Workflow entity with details. Version: {Version}, Name: {Name}, StepIds: {StepIds}, CreatedBy: {CreatedBy}, User: {User}, RequestId: {RequestId}",
                entity.Version, entity.Name, string.Join(",", entity.StepIds), entity.CreatedBy, userContext, HttpContext.TraceIdentifier);

            var created = await _repository.CreateAsync(entity);

            if (created.Id == Guid.Empty)
            {
                _logger.LogErrorWithCorrelation("MongoDB failed to generate ID for new WorkflowEntity. Version: {Version}, Name: {Name}, User: {User}, RequestId: {RequestId}",
                    entity.Version, entity.Name, userContext, HttpContext.TraceIdentifier);
                return StatusCode(500, "Failed to generate entity ID");
            }

            _logger.LogInformationWithCorrelation("Successfully created Workflow entity. Id: {Id}, Version: {Version}, Name: {Name}, StepIds: {StepIds}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                created.Id, created.Version, created.Name, string.Join(",", created.StepIds), created.GetCompositeKey(), userContext, HttpContext.TraceIdentifier);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("step") && (ex.Message.Contains("do not exist") || ex.Message.Contains("validation service")))
        {
            _logger.LogWarningWithCorrelation(ex, "Step validation failed creating Workflow entity. StepIds: {StepIds}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                string.Join(",", entity?.StepIds ?? new List<Guid>()), entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return BadRequest(new {
                error = "Step validation failed",
                message = ex.Message,
                stepIds = entity?.StepIds
            });
        }
        catch (DuplicateKeyException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Duplicate key conflict creating Workflow entity. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error creating Workflow entity. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while creating the Workflow entity");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<WorkflowEntity>> Update(string id, [FromBody] WorkflowEntity entity)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        var compositeKey = entity?.GetCompositeKey() ?? "Unknown";

        // Validate GUID format
        if (!Guid.TryParse(id, out Guid guidId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided for update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                id, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {id}");
        }

        _logger.LogInformationWithCorrelation("Starting Update Workflow request. Id: {Id}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            guidId, compositeKey, userContext, HttpContext.TraceIdentifier);

        if (entity == null)
        {
            _logger.LogWarningWithCorrelation("Null entity provided for Workflow update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return BadRequest("Workflow entity cannot be null");
        }

        if (guidId != entity.Id)
        {
            _logger.LogWarningWithCorrelation("ID mismatch in Workflow update request. URL ID: {UrlId}, Entity ID: {EntityId}, User: {User}, RequestId: {RequestId}",
                guidId, entity.Id, userContext, HttpContext.TraceIdentifier);
            return BadRequest("ID in URL does not match entity ID");
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarningWithCorrelation("Invalid model state for Workflow update. Id: {Id}, Errors: {Errors}, User: {User}, RequestId: {RequestId}",
                guidId, errorMessage, userContext, HttpContext.TraceIdentifier);
            return BadRequest(ModelState);
        }

        try
        {
            var existingEntity = await _repository.GetByIdAsync(guidId);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Workflow entity not found for update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Workflow entity with ID {guidId} not found");
            }

            // Check referential integrity with OrchestratedFlow entities if workflow ID is changing
            if (existingEntity.Id != entity.Id)
            {
                _logger.LogInformationWithCorrelation("Checking referential integrity for Workflow update (ID change). Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);

                var hasOrchestratedFlowReferences = await _orchestratedFlowValidationService.CheckWorkflowReferencesAsync(guidId);
                if (hasOrchestratedFlowReferences)
                {
                    _logger.LogWarningWithCorrelation("Cannot update workflow ID - referenced by OrchestratedFlow entities. Id: {Id}, User: {User}, RequestId: {RequestId}",
                        guidId, userContext, HttpContext.TraceIdentifier);
                    return Conflict("Cannot update workflow ID: it is referenced by one or more OrchestratedFlow entities");
                }
            }

            // Validate step IDs exist before updating the entity
            _logger.LogDebugWithCorrelation("Validating step references before updating Workflow entity. StepIds: {StepIds}, User: {User}, RequestId: {RequestId}",
                string.Join(",", entity.StepIds), userContext, HttpContext.TraceIdentifier);

            await _workflowValidator.ValidateStepsExist(entity.StepIds);

            entity.UpdatedBy = userContext;
            entity.CreatedAt = existingEntity.CreatedAt;
            entity.CreatedBy = existingEntity.CreatedBy;

            _logger.LogDebugWithCorrelation("All validations passed. Updating Workflow entity with details. Id: {Id}, Version: {Version}, Name: {Name}, StepIds: {StepIds}, UpdatedBy: {UpdatedBy}, User: {User}, RequestId: {RequestId}",
                entity.Id, entity.Version, entity.Name, string.Join(",", entity.StepIds), entity.UpdatedBy, userContext, HttpContext.TraceIdentifier);

            var updated = await _repository.UpdateAsync(entity);

            _logger.LogInformationWithCorrelation("Successfully updated Workflow entity. Id: {Id}, Version: {Version}, Name: {Name}, StepIds: {StepIds}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                updated.Id, updated.Version, updated.Name, string.Join(",", updated.StepIds), updated.GetCompositeKey(), userContext, HttpContext.TraceIdentifier);

            return Ok(updated);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("step") && (ex.Message.Contains("do not exist") || ex.Message.Contains("validation service")))
        {
            _logger.LogWarningWithCorrelation(ex, "Step validation failed updating Workflow entity. Id: {Id}, StepIds: {StepIds}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, string.Join(",", entity?.StepIds ?? new List<Guid>()), entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return BadRequest(new {
                error = "Step validation failed",
                message = ex.Message,
                stepIds = entity?.StepIds
            });
        }
        catch (DuplicateKeyException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Duplicate key conflict updating Workflow entity. Id: {Id}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error updating Workflow entity. Id: {Id}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while updating the Workflow entity");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(id, out Guid guidId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided for delete. Id: {Id}, User: {User}, RequestId: {RequestId}",
                id, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {id}");
        }

        _logger.LogInformationWithCorrelation("Starting Delete Workflow request. Id: {Id}, User: {User}, RequestId: {RequestId}",
            guidId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(guidId);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Workflow entity not found for deletion. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Workflow entity with ID {guidId} not found");
            }

            // Check referential integrity with OrchestratedFlow entities
            _logger.LogInformationWithCorrelation("Checking referential integrity for Workflow deletion. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);

            var hasOrchestratedFlowReferences = await _orchestratedFlowValidationService.CheckWorkflowReferencesAsync(guidId);
            if (hasOrchestratedFlowReferences)
            {
                _logger.LogWarningWithCorrelation("Cannot delete workflow - referenced by OrchestratedFlow entities. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return Conflict("Cannot delete workflow: it is referenced by one or more OrchestratedFlow entities");
            }

            _logger.LogDebugWithCorrelation("Deleting Workflow entity. Id: {Id}, Version: {Version}, Name: {Name}, StepIds: {StepIds}, User: {User}, RequestId: {RequestId}",
                guidId, existingEntity.Version, existingEntity.Name, string.Join(",", existingEntity.StepIds), userContext, HttpContext.TraceIdentifier);

            var success = await _repository.DeleteAsync(guidId);

            if (!success)
            {
                _logger.LogErrorWithCorrelation("Failed to delete Workflow entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return StatusCode(500, "Failed to delete Workflow entity");
            }

            _logger.LogInformationWithCorrelation("Successfully deleted Workflow entity. Id: {Id}, Version: {Version}, Name: {Name}, StepIds: {StepIds}, User: {User}, RequestId: {RequestId}",
                guidId, existingEntity.Version, existingEntity.Name, string.Join(",", existingEntity.StepIds), userContext, HttpContext.TraceIdentifier);

            return NoContent();
        }
        
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error deleting Workflow entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while deleting the Workflow entity");
        }
    }

    [HttpGet("composite")]
    public ActionResult<WorkflowEntity> GetByCompositeKeyEmpty()
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        _logger.LogWarningWithCorrelation("Empty composite key parameters in GetByCompositeKey request (no parameters provided). User: {User}, RequestId: {RequestId}",
            userContext, HttpContext.TraceIdentifier);

        return BadRequest(new {
            error = "Invalid composite key parameters",
            message = "Both version and name parameters are required",
            parameters = new[] { "version", "name" }
        });
    }
}
