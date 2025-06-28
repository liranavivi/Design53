using Manager.Assignment.Repositories;
using Manager.Assignment.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Correlation;
using Shared.Entities;
using Shared.Exceptions;
using Swashbuckle.AspNetCore.Annotations;

namespace Manager.Assignment.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Assignment management operations")]
public class AssignmentController : ControllerBase
{
    private readonly IAssignmentEntityRepository _repository;
    private readonly IOrchestratedFlowValidationService _orchestratedFlowValidationService;
    private readonly IAssignmentValidationService _assignmentValidator;
    private readonly ILogger<AssignmentController> _logger;

    public AssignmentController(
        IAssignmentEntityRepository repository,
        IOrchestratedFlowValidationService orchestratedFlowValidationService,
        IAssignmentValidationService assignmentValidator,
        ILogger<AssignmentController> logger)
    {
        _repository = repository;
        _orchestratedFlowValidationService = orchestratedFlowValidationService;
        _assignmentValidator = assignmentValidator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AssignmentEntity>>> GetAll()
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetAll schemas request. User: {User}, RequestId: {RequestId}",
            userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetAllAsync();
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Assignment entities. User: {User}, RequestId: {RequestId}",
                count, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving all Assignment entities. User: {User}, RequestId: {RequestId}",
                userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Assignment entities");
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

            _logger.LogInformationWithCorrelation("Successfully retrieved paged Assignment entities. Page: {Page}/{TotalPages}, PageSize: {PageSize}, TotalCount: {TotalCount}, User: {User}, RequestId: {RequestId}",
                page, totalPages, pageSize, totalCount, userContext, HttpContext.TraceIdentifier);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving paged Assignment entities. Page: {Page}, PageSize: {PageSize}, User: {User}, RequestId: {RequestId}",
                originalPage, originalPageSize, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving paged Assignment entities");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AssignmentEntity>> GetById(string id)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(id, out Guid guidId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided. Id: {Id}, User: {User}, RequestId: {RequestId}",
                id, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {id}");
        }

        _logger.LogInformationWithCorrelation("Starting GetById Assignment request. Id: {Id}, User: {User}, RequestId: {RequestId}",
            guidId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entity = await _repository.GetByIdAsync(guidId);

            if (entity == null)
            {
                _logger.LogWarningWithCorrelation("Assignment entity not found. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Assignment entity with ID {guidId} not found");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved Assignment entity. Id: {Id}, Version: {Version}, Name: {Name}, StepId: {StepId}, EntityIds count: {EntityIdsCount}, User: {User}, RequestId: {RequestId}",
                entity.Id, entity.Version, entity.Name, entity.StepId, entity.EntityIds?.Count ?? 0, userContext, HttpContext.TraceIdentifier);

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Assignment entity by ID. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving the Assignment entity");
        }
    }

    [HttpGet("composite/{version}/{name}")]
    public async Task<ActionResult<AssignmentEntity>> GetByCompositeKey(string version, string name)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        // AssignmentEntity composite key format: "version_name"
        var compositeKey = $"{version}_{name}";

        _logger.LogInformationWithCorrelation("Starting GetByCompositeKey Assignment request. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
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
                _logger.LogWarningWithCorrelation("Assignment entity not found by composite key. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                    version, name, compositeKey, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Assignment entity with version '{version}' and name '{name}' not found");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved Assignment entity by composite key. CompositeKey: {CompositeKey}, Id: {Id}, Version: {Version}, Name: {Name}, StepId: {StepId}, EntityIds count: {EntityIdsCount}, User: {User}, RequestId: {RequestId}",
                compositeKey, entity.Id, entity.Version, entity.Name, entity.StepId, entity.EntityIds?.Count ?? 0, userContext, HttpContext.TraceIdentifier);

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Assignment entity by composite key. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                version, name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving the Assignment entity");
        }
    }

    [HttpGet("step/{stepId}")]
    public async Task<ActionResult<IEnumerable<AssignmentEntity>>> GetByStepId(string stepId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(stepId, out Guid guidStepId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided for GetByStepId. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
                stepId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {stepId}");
        }

        _logger.LogInformationWithCorrelation("Starting GetByStepId Assignment request. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
            guidStepId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetByStepIdAsync(guidStepId);
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Assignment entities by step ID. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
                count, guidStepId, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Assignment entities by step ID. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
                guidStepId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Assignment entities");
        }
    }

    [HttpGet("entity/{entityId}")]
    public async Task<ActionResult<IEnumerable<AssignmentEntity>>> GetByEntityId(string entityId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(entityId, out Guid guidEntityId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided for GetByEntityId. EntityId: {EntityId}, User: {User}, RequestId: {RequestId}",
                entityId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {entityId}");
        }

        _logger.LogInformationWithCorrelation("Starting GetByEntityId Assignment request. EntityId: {EntityId}, User: {User}, RequestId: {RequestId}",
            guidEntityId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetByEntityIdAsync(guidEntityId);
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Assignment entities by entity ID. EntityId: {EntityId}, User: {User}, RequestId: {RequestId}",
                count, guidEntityId, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Assignment entities by entity ID. EntityId: {EntityId}, User: {User}, RequestId: {RequestId}",
                guidEntityId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Assignment entities");
        }
    }

    [HttpGet("entity/{entityId}/exists")]
    public async Task<ActionResult<bool>> CheckEntityExists(string entityId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(entityId, out Guid guidEntityId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided for CheckEntityExists. EntityId: {EntityId}, User: {User}, RequestId: {RequestId}",
                entityId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {entityId}");
        }

        _logger.LogInformationWithCorrelation("Starting CheckEntityExists Assignment request. EntityId: {EntityId}, User: {User}, RequestId: {RequestId}",
            guidEntityId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var hasReferences = await _repository.HasEntityReferences(guidEntityId);

            _logger.LogInformationWithCorrelation("Successfully checked entity references. EntityId: {EntityId}, HasReferences: {HasReferences}, User: {User}, RequestId: {RequestId}",
                guidEntityId, hasReferences, userContext, HttpContext.TraceIdentifier);

            return Ok(hasReferences);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error occurred while checking entity references. EntityId: {EntityId}, User: {User}, RequestId: {RequestId}",
                guidEntityId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while processing the request");
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

        _logger.LogInformationWithCorrelation("Starting CheckStepReferences Assignment request. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
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
    public async Task<ActionResult<IEnumerable<AssignmentEntity>>> GetByVersion(string version)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByVersion Assignment request. Version: {Version}, User: {User}, RequestId: {RequestId}",
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

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Assignment entities by version. Version: {Version}, User: {User}, RequestId: {RequestId}",
                count, version, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Assignment entities by version. Version: {Version}, User: {User}, RequestId: {RequestId}",
                version, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Assignment entities");
        }
    }

    [HttpGet("name/{name}")]
    public async Task<ActionResult<IEnumerable<AssignmentEntity>>> GetByName(string name)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByName Assignment request. Name: {Name}, User: {User}, RequestId: {RequestId}",
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

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Assignment entities by name. Name: {Name}, User: {User}, RequestId: {RequestId}",
                count, name, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Assignment entities by name. Name: {Name}, User: {User}, RequestId: {RequestId}",
                name, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Assignment entities");
        }
    }

    [HttpPost]
    public async Task<ActionResult<AssignmentEntity>> Create([FromBody] AssignmentEntity entity)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        var compositeKey = entity?.GetCompositeKey() ?? "Unknown";

        _logger.LogInformationWithCorrelation("Starting Create Assignment request. CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            compositeKey, userContext, HttpContext.TraceIdentifier);

        if (entity == null)
        {
            _logger.LogWarningWithCorrelation("Null entity provided for Assignment creation. User: {User}, RequestId: {RequestId}",
                userContext, HttpContext.TraceIdentifier);
            return BadRequest("Assignment entity cannot be null");
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarningWithCorrelation("Invalid model state for Assignment creation. Errors: {Errors}, User: {User}, RequestId: {RequestId}",
                errorMessage, userContext, HttpContext.TraceIdentifier);
            return BadRequest(ModelState);
        }

        try
        {
            entity!.CreatedBy = userContext;
            entity.Id = Guid.Empty;

            // Validate step exists before creating the entity
            _logger.LogDebugWithCorrelation("Validating step reference before creating Assignment entity. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
                entity.StepId, userContext, HttpContext.TraceIdentifier);

            await _assignmentValidator.ValidateStepExists(entity.StepId);

            // Validate entity IDs exist before creating the entity
            _logger.LogDebugWithCorrelation("Validating entity references before creating Assignment entity. EntityIds: {EntityIds}, User: {User}, RequestId: {RequestId}",
                string.Join(",", entity.EntityIds), userContext, HttpContext.TraceIdentifier);

            await _assignmentValidator.ValidateEntitiesExist(entity.EntityIds);

            _logger.LogDebugWithCorrelation("All validations passed. Creating Assignment entity with details. Version: {Version}, Name: {Name}, StepId: {StepId}, EntityIds count: {EntityIdsCount}, CreatedBy: {CreatedBy}, User: {User}, RequestId: {RequestId}",
                entity.Version, entity.Name, entity.StepId, entity.EntityIds?.Count ?? 0, entity.CreatedBy, userContext, HttpContext.TraceIdentifier);

            var created = await _repository.CreateAsync(entity);

            if (created.Id == Guid.Empty)
            {
                _logger.LogErrorWithCorrelation("MongoDB failed to generate ID for new AssignmentEntity. Version: {Version}, Name: {Name}, User: {User}, RequestId: {RequestId}",
                    entity.Version, entity.Name, userContext, HttpContext.TraceIdentifier);
                return StatusCode(500, "Failed to generate entity ID");
            }

            _logger.LogInformationWithCorrelation("Successfully created Assignment entity. Id: {Id}, Version: {Version}, Name: {Name}, StepId: {StepId}, EntityIds count: {EntityIdsCount}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                created.Id, created.Version, created.Name, created.StepId, created.EntityIds?.Count ?? 0, created.GetCompositeKey(), userContext, HttpContext.TraceIdentifier);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Step") || ex.Message.Contains("step"))
        {
            _logger.LogWarningWithCorrelation(ex, "Step validation failed creating Assignment entity. StepId: {StepId}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.StepId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return BadRequest(new {
                error = "Step validation failed",
                message = ex.Message,
                stepId = entity?.StepId
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("entity") && (ex.Message.Contains("do not exist") || ex.Message.Contains("validation service")))
        {
            _logger.LogWarningWithCorrelation(ex, "Entity validation failed creating Assignment entity. EntityIds: {EntityIds}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                string.Join(",", entity?.EntityIds ?? new List<Guid>()), entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return BadRequest(new {
                error = "Entity validation failed",
                message = ex.Message,
                entityIds = entity?.EntityIds
            });
        }
        catch (DuplicateKeyException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Duplicate key conflict creating Assignment entity. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error creating Assignment entity. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while creating the Assignment entity");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<AssignmentEntity>> Update(string id, [FromBody] AssignmentEntity entity)
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

        _logger.LogInformationWithCorrelation("Starting Update Assignment request. Id: {Id}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            guidId, compositeKey, userContext, HttpContext.TraceIdentifier);

        if (entity == null)
        {
            _logger.LogWarningWithCorrelation("Null entity provided for Assignment update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return BadRequest("Assignment entity cannot be null");
        }

        if (guidId != entity.Id)
        {
            _logger.LogWarningWithCorrelation("ID mismatch in Assignment update request. URL ID: {UrlId}, Entity ID: {EntityId}, User: {User}, RequestId: {RequestId}",
                guidId, entity.Id, userContext, HttpContext.TraceIdentifier);
            return BadRequest("ID in URL does not match entity ID");
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarningWithCorrelation("Invalid model state for Assignment update. Id: {Id}, Errors: {Errors}, User: {User}, RequestId: {RequestId}",
                guidId, errorMessage, userContext, HttpContext.TraceIdentifier);
            return BadRequest(ModelState);
        }

        try
        {
            var existingEntity = await _repository.GetByIdAsync(guidId);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Assignment entity not found for update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Assignment entity with ID {guidId} not found");
            }

            // Check referential integrity with OrchestratedFlow entities if assignment ID is changing
            if (existingEntity.Id != entity.Id)
            {
                _logger.LogInformationWithCorrelation("Checking referential integrity for Assignment update (ID change). Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);

                var hasOrchestratedFlowReferences = await _orchestratedFlowValidationService.CheckAssignmentReferencesAsync(guidId);
                if (hasOrchestratedFlowReferences)
                {
                    _logger.LogWarningWithCorrelation("Cannot update assignment ID - referenced by OrchestratedFlow entities. Id: {Id}, User: {User}, RequestId: {RequestId}",
                        guidId, userContext, HttpContext.TraceIdentifier);
                    return Conflict("Cannot update assignment ID: it is referenced by one or more OrchestratedFlow entities");
                }
            }

            // Validate step exists before updating the entity
            _logger.LogDebugWithCorrelation("Validating step reference before updating Assignment entity. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
                entity.StepId, userContext, HttpContext.TraceIdentifier);

            await _assignmentValidator.ValidateStepExists(entity.StepId);

            // Validate entity IDs exist before updating the entity
            _logger.LogDebugWithCorrelation("Validating entity references before updating Assignment entity. EntityIds: {EntityIds}, User: {User}, RequestId: {RequestId}",
                string.Join(",", entity.EntityIds), userContext, HttpContext.TraceIdentifier);

            await _assignmentValidator.ValidateEntitiesExist(entity.EntityIds);

            entity.UpdatedBy = userContext;
            entity.CreatedAt = existingEntity.CreatedAt;
            entity.CreatedBy = existingEntity.CreatedBy;

            _logger.LogDebugWithCorrelation("All validations passed. Updating Assignment entity with details. Id: {Id}, Version: {Version}, Name: {Name}, StepId: {StepId}, EntityIds count: {EntityIdsCount}, UpdatedBy: {UpdatedBy}, User: {User}, RequestId: {RequestId}",
                entity.Id, entity.Version, entity.Name, entity.StepId, entity.EntityIds?.Count ?? 0, entity.UpdatedBy, userContext, HttpContext.TraceIdentifier);

            var updated = await _repository.UpdateAsync(entity);

            _logger.LogInformationWithCorrelation("Successfully updated Assignment entity. Id: {Id}, Version: {Version}, Name: {Name}, StepId: {StepId}, EntityIds count: {EntityIdsCount}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                updated.Id, updated.Version, updated.Name, updated.StepId, updated.EntityIds?.Count ?? 0, updated.GetCompositeKey(), userContext, HttpContext.TraceIdentifier);

            return Ok(updated);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Step") || ex.Message.Contains("step"))
        {
            _logger.LogWarningWithCorrelation(ex, "Step validation failed updating Assignment entity. Id: {Id}, StepId: {StepId}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.StepId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return BadRequest(new {
                error = "Step validation failed",
                message = ex.Message,
                stepId = entity?.StepId
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("entity") && (ex.Message.Contains("do not exist") || ex.Message.Contains("validation service")))
        {
            _logger.LogWarningWithCorrelation(ex, "Entity validation failed updating Assignment entity. Id: {Id}, EntityIds: {EntityIds}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, string.Join(",", entity?.EntityIds ?? new List<Guid>()), entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return BadRequest(new {
                error = "Entity validation failed",
                message = ex.Message,
                entityIds = entity?.EntityIds
            });
        }
        catch (DuplicateKeyException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Duplicate key conflict updating Assignment entity. Id: {Id}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error updating Assignment entity. Id: {Id}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while updating the Assignment entity");
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

        _logger.LogInformationWithCorrelation("Starting Delete Assignment request. Id: {Id}, User: {User}, RequestId: {RequestId}",
            guidId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(guidId);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Assignment entity not found for deletion. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Assignment entity with ID {guidId} not found");
            }

            // Check referential integrity with OrchestratedFlow entities
            _logger.LogInformationWithCorrelation("Checking referential integrity for Assignment deletion. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);

            var hasOrchestratedFlowReferences = await _orchestratedFlowValidationService.CheckAssignmentReferencesAsync(guidId);
            if (hasOrchestratedFlowReferences)
            {
                _logger.LogWarningWithCorrelation("Cannot delete assignment - referenced by OrchestratedFlow entities. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return Conflict("Cannot delete assignment: it is referenced by one or more OrchestratedFlow entities");
            }

            _logger.LogDebugWithCorrelation("Deleting Assignment entity. Id: {Id}, Version: {Version}, Name: {Name}, StepId: {StepId}, EntityIds count: {EntityIdsCount}, User: {User}, RequestId: {RequestId}",
                guidId, existingEntity.Version, existingEntity.Name, existingEntity.StepId, existingEntity.EntityIds?.Count ?? 0, userContext, HttpContext.TraceIdentifier);

            var success = await _repository.DeleteAsync(guidId);

            if (!success)
            {
                _logger.LogErrorWithCorrelation("Failed to delete Assignment entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return StatusCode(500, "Failed to delete Assignment entity");
            }

            _logger.LogInformationWithCorrelation("Successfully deleted Assignment entity. Id: {Id}, Version: {Version}, Name: {Name}, StepId: {StepId}, EntityIds count: {EntityIdsCount}, User: {User}, RequestId: {RequestId}",
                guidId, existingEntity.Version, existingEntity.Name, existingEntity.StepId, existingEntity.EntityIds?.Count ?? 0, userContext, HttpContext.TraceIdentifier);

            return NoContent();
        }
        
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error deleting Assignment entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while deleting the Assignment entity");
        }
    }

    [HttpGet("composite")]
    public ActionResult<AssignmentEntity> GetByCompositeKeyEmpty()
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
