using Manager.OrchestratedFlow.Repositories;
using Manager.OrchestratedFlow.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Correlation;
using Shared.Entities;
using Shared.Exceptions;
using Swashbuckle.AspNetCore.Annotations;

namespace Manager.OrchestratedFlow.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("OrchestratedFlow management operations")]
public class OrchestratedFlowController : ControllerBase
{
    private readonly IOrchestratedFlowEntityRepository _repository;
    private readonly IWorkflowValidationService _workflowValidationService;
    private readonly IAssignmentValidationService _assignmentValidationService;
    private readonly ILogger<OrchestratedFlowController> _logger;

    public OrchestratedFlowController(
        IOrchestratedFlowEntityRepository repository,
        IWorkflowValidationService workflowValidationService,
        IAssignmentValidationService assignmentValidationService,
        ILogger<OrchestratedFlowController> logger)
    {
        _repository = repository;
        _workflowValidationService = workflowValidationService;
        _assignmentValidationService = assignmentValidationService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrchestratedFlowEntity>>> GetAll()
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetAll schemas request. User: {User}, RequestId: {RequestId}",
            userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetAllAsync();
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} OrchestratedFlow entities. User: {User}, RequestId: {RequestId}",
                count, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving all OrchestratedFlow entities. User: {User}, RequestId: {RequestId}",
                userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving OrchestratedFlow entities");
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

            _logger.LogInformationWithCorrelation("Successfully retrieved paged OrchestratedFlow entities. Page: {Page}/{TotalPages}, PageSize: {PageSize}, TotalCount: {TotalCount}, User: {User}, RequestId: {RequestId}",
                page, totalPages, pageSize, totalCount, userContext, HttpContext.TraceIdentifier);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving paged OrchestratedFlow entities. Page: {Page}, PageSize: {PageSize}, User: {User}, RequestId: {RequestId}",
                originalPage, originalPageSize, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving paged OrchestratedFlow entities");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrchestratedFlowEntity>> GetById(string id)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(id, out Guid guidId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided. Id: {Id}, User: {User}, RequestId: {RequestId}",
                id, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {id}");
        }

        _logger.LogInformationWithCorrelation("Starting GetById OrchestratedFlow request. Id: {Id}, User: {User}, RequestId: {RequestId}",
            guidId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entity = await _repository.GetByIdAsync(guidId);

            if (entity == null)
            {
                _logger.LogWarningWithCorrelation("OrchestratedFlow entity not found. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"OrchestratedFlow entity with ID {guidId} not found");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved OrchestratedFlow entity. Id: {Id}, Version: {Version}, Name: {Name}, WorkflowId: {WorkflowId}, AssignmentIds: {AssignmentIds}, User: {User}, RequestId: {RequestId}",
                entity.Id, entity.Version, entity.Name, entity.WorkflowId, string.Join(",", entity.AssignmentIds), userContext, HttpContext.TraceIdentifier);

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving OrchestratedFlow entity by ID. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving the OrchestratedFlow entity");
        }
    }

    [HttpGet("composite/{version}/{name}")]
    public async Task<ActionResult<OrchestratedFlowEntity>> GetByCompositeKey(string version, string name)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        // OrchestratedFlowEntity composite key format: "version_name"
        var compositeKey = $"{version}_{name}";

        _logger.LogInformationWithCorrelation("Starting GetByCompositeKey OrchestratedFlow request. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
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
                _logger.LogWarningWithCorrelation("OrchestratedFlow entity not found by composite key. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                    version, name, compositeKey, userContext, HttpContext.TraceIdentifier);
                return NotFound($"OrchestratedFlow entity with version '{version}' and name '{name}' not found");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved OrchestratedFlow entity by composite key. CompositeKey: {CompositeKey}, Id: {Id}, Version: {Version}, Name: {Name}, WorkflowId: {WorkflowId}, AssignmentIds: {AssignmentIds}, User: {User}, RequestId: {RequestId}",
                compositeKey, entity.Id, entity.Version, entity.Name, entity.WorkflowId, string.Join(",", entity.AssignmentIds), userContext, HttpContext.TraceIdentifier);

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving OrchestratedFlow entity by composite key. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                version, name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving the OrchestratedFlow entity");
        }
    }

    [HttpGet("workflow/{workflowId}")]
    public async Task<ActionResult<IEnumerable<OrchestratedFlowEntity>>> GetByWorkflowId(string workflowId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(workflowId, out Guid guidWorkflowId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided. WorkflowId: {WorkflowId}, User: {User}, RequestId: {RequestId}",
                workflowId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {workflowId}");
        }

        _logger.LogInformationWithCorrelation("Starting GetByWorkflowId OrchestratedFlow request. WorkflowId: {WorkflowId}, User: {User}, RequestId: {RequestId}",
            guidWorkflowId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetByWorkflowIdAsync(guidWorkflowId);
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} OrchestratedFlow entities by workflow ID. WorkflowId: {WorkflowId}, User: {User}, RequestId: {RequestId}",
                count, guidWorkflowId, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving OrchestratedFlow entities by workflow ID. WorkflowId: {WorkflowId}, User: {User}, RequestId: {RequestId}",
                guidWorkflowId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving OrchestratedFlow entities");
        }
    }

    [HttpGet("assignment/{assignmentId}")]
    public async Task<ActionResult<IEnumerable<OrchestratedFlowEntity>>> GetByAssignmentId(string assignmentId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(assignmentId, out Guid guidAssignmentId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided. AssignmentId: {AssignmentId}, User: {User}, RequestId: {RequestId}",
                assignmentId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {assignmentId}");
        }

        _logger.LogInformationWithCorrelation("Starting GetByAssignmentId OrchestratedFlow request. AssignmentId: {AssignmentId}, User: {User}, RequestId: {RequestId}",
            guidAssignmentId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetByAssignmentIdAsync(guidAssignmentId);
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} OrchestratedFlow entities by assignment ID. AssignmentId: {AssignmentId}, User: {User}, RequestId: {RequestId}",
                count, guidAssignmentId, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving OrchestratedFlow entities by assignment ID. AssignmentId: {AssignmentId}, User: {User}, RequestId: {RequestId}",
                guidAssignmentId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving OrchestratedFlow entities");
        }
    }

    [HttpGet("version/{version}")]
    public async Task<ActionResult<IEnumerable<OrchestratedFlowEntity>>> GetByVersion(string version)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByVersion OrchestratedFlow request. Version: {Version}, User: {User}, RequestId: {RequestId}",
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

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} OrchestratedFlow entities by version. Version: {Version}, User: {User}, RequestId: {RequestId}",
                count, version, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving OrchestratedFlow entities by version. Version: {Version}, User: {User}, RequestId: {RequestId}",
                version, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving OrchestratedFlow entities");
        }
    }

    [HttpGet("name/{name}")]
    public async Task<ActionResult<IEnumerable<OrchestratedFlowEntity>>> GetByName(string name)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByName OrchestratedFlow request. Name: {Name}, User: {User}, RequestId: {RequestId}",
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

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} OrchestratedFlow entities by name. Name: {Name}, User: {User}, RequestId: {RequestId}",
                count, name, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving OrchestratedFlow entities by name. Name: {Name}, User: {User}, RequestId: {RequestId}",
                name, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving OrchestratedFlow entities");
        }
    }

    [HttpGet("workflow/{workflowId}/exists")]
    public async Task<ActionResult<bool>> CheckWorkflowReferences(string workflowId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(workflowId, out Guid guidWorkflowId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided. WorkflowId: {WorkflowId}, User: {User}, RequestId: {RequestId}",
                workflowId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {workflowId}");
        }

        _logger.LogInformationWithCorrelation("Starting CheckWorkflowReferences OrchestratedFlow request. WorkflowId: {WorkflowId}, User: {User}, RequestId: {RequestId}",
            guidWorkflowId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var hasReferences = await _repository.HasWorkflowReferences(guidWorkflowId);

            _logger.LogInformationWithCorrelation("Successfully checked workflow references. WorkflowId: {WorkflowId}, HasReferences: {HasReferences}, User: {User}, RequestId: {RequestId}",
                guidWorkflowId, hasReferences, userContext, HttpContext.TraceIdentifier);

            return Ok(hasReferences);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error checking workflow references. WorkflowId: {WorkflowId}, User: {User}, RequestId: {RequestId}",
                guidWorkflowId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while checking workflow references");
        }
    }

    [HttpGet("assignment/{assignmentId}/exists")]
    public async Task<ActionResult<bool>> CheckAssignmentReferences(string assignmentId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(assignmentId, out Guid guidAssignmentId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided. AssignmentId: {AssignmentId}, User: {User}, RequestId: {RequestId}",
                assignmentId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {assignmentId}");
        }

        _logger.LogInformationWithCorrelation("Starting CheckAssignmentReferences OrchestratedFlow request. AssignmentId: {AssignmentId}, User: {User}, RequestId: {RequestId}",
            guidAssignmentId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var hasReferences = await _repository.HasAssignmentReferences(guidAssignmentId);

            _logger.LogInformationWithCorrelation("Successfully checked assignment references. AssignmentId: {AssignmentId}, HasReferences: {HasReferences}, User: {User}, RequestId: {RequestId}",
                guidAssignmentId, hasReferences, userContext, HttpContext.TraceIdentifier);

            return Ok(hasReferences);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error checking assignment references. AssignmentId: {AssignmentId}, User: {User}, RequestId: {RequestId}",
                guidAssignmentId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while checking assignment references");
        }
    }

    [HttpPost]
    public async Task<ActionResult<OrchestratedFlowEntity>> Create([FromBody] OrchestratedFlowEntity entity)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        var compositeKey = entity?.GetCompositeKey() ?? "Unknown";

        _logger.LogInformationWithCorrelation("Starting Create OrchestratedFlow request. CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            compositeKey, userContext, HttpContext.TraceIdentifier);

        if (entity == null)
        {
            _logger.LogWarningWithCorrelation("Null entity provided for OrchestratedFlow creation. User: {User}, RequestId: {RequestId}",
                userContext, HttpContext.TraceIdentifier);
            return BadRequest("OrchestratedFlow entity cannot be null");
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarningWithCorrelation("Invalid model state for OrchestratedFlow creation. Errors: {Errors}, User: {User}, RequestId: {RequestId}",
                errorMessage, userContext, HttpContext.TraceIdentifier);
            return BadRequest(ModelState);
        }

        try
        {
            entity!.CreatedBy = userContext;
            entity.Id = Guid.Empty;

            // Validate referential integrity - check that WorkflowId and AssignmentIds exist
            _logger.LogInformationWithCorrelation("Validating referential integrity for OrchestratedFlow creation. WorkflowId: {WorkflowId}, AssignmentIds: {AssignmentIds}, User: {User}, RequestId: {RequestId}",
                entity.WorkflowId, string.Join(",", entity.AssignmentIds), userContext, HttpContext.TraceIdentifier);

            var workflowExists = await _workflowValidationService.ValidateWorkflowExistsAsync(entity.WorkflowId);
            if (!workflowExists)
            {
                _logger.LogWarningWithCorrelation("Cannot create OrchestratedFlow - referenced workflow does not exist. WorkflowId: {WorkflowId}, User: {User}, RequestId: {RequestId}",
                    entity.WorkflowId, userContext, HttpContext.TraceIdentifier);
                return BadRequest($"Referenced workflow with ID {entity.WorkflowId} does not exist");
            }

            var assignmentsExist = await _assignmentValidationService.ValidateAssignmentsExistAsync(entity.AssignmentIds);
            if (!assignmentsExist)
            {
                _logger.LogWarningWithCorrelation("Cannot create OrchestratedFlow - one or more referenced assignments do not exist. AssignmentIds: {AssignmentIds}, User: {User}, RequestId: {RequestId}",
                    string.Join(",", entity.AssignmentIds), userContext, HttpContext.TraceIdentifier);
                return BadRequest("One or more referenced assignments do not exist");
            }

            _logger.LogDebugWithCorrelation("Creating OrchestratedFlow entity with details. Version: {Version}, Name: {Name}, WorkflowId: {WorkflowId}, AssignmentIds: {AssignmentIds}, CreatedBy: {CreatedBy}, User: {User}, RequestId: {RequestId}",
                entity.Version, entity.Name, entity.WorkflowId, string.Join(",", entity.AssignmentIds), entity.CreatedBy, userContext, HttpContext.TraceIdentifier);

            var created = await _repository.CreateAsync(entity);

            if (created.Id == Guid.Empty)
            {
                _logger.LogErrorWithCorrelation("MongoDB failed to generate ID for new OrchestratedFlowEntity. Version: {Version}, Name: {Name}, User: {User}, RequestId: {RequestId}",
                    entity.Version, entity.Name, userContext, HttpContext.TraceIdentifier);
                return StatusCode(500, "Failed to generate entity ID");
            }

            _logger.LogInformationWithCorrelation("Successfully created OrchestratedFlow entity. Id: {Id}, Version: {Version}, Name: {Name}, WorkflowId: {WorkflowId}, AssignmentIds: {AssignmentIds}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                created.Id, created.Version, created.Name, created.WorkflowId, string.Join(",", created.AssignmentIds), created.GetCompositeKey(), userContext, HttpContext.TraceIdentifier);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (DuplicateKeyException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Duplicate key conflict creating OrchestratedFlow entity. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error creating OrchestratedFlow entity. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while creating the OrchestratedFlow entity");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<OrchestratedFlowEntity>> Update(string id, [FromBody] OrchestratedFlowEntity entity)
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

        _logger.LogInformationWithCorrelation("Starting Update OrchestratedFlow request. Id: {Id}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            guidId, compositeKey, userContext, HttpContext.TraceIdentifier);

        if (entity == null)
        {
            _logger.LogWarningWithCorrelation("Null entity provided for OrchestratedFlow update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return BadRequest("OrchestratedFlow entity cannot be null");
        }

        if (guidId != entity.Id)
        {
            _logger.LogWarningWithCorrelation("ID mismatch in OrchestratedFlow update request. URL ID: {UrlId}, Entity ID: {EntityId}, User: {User}, RequestId: {RequestId}",
                guidId, entity.Id, userContext, HttpContext.TraceIdentifier);
            return BadRequest("ID in URL does not match entity ID");
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarningWithCorrelation("Invalid model state for OrchestratedFlow update. Id: {Id}, Errors: {Errors}, User: {User}, RequestId: {RequestId}",
                guidId, errorMessage, userContext, HttpContext.TraceIdentifier);
            return BadRequest(ModelState);
        }

        try
        {
            var existingEntity = await _repository.GetByIdAsync(guidId);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("OrchestratedFlow entity not found for update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"OrchestratedFlow entity with ID {guidId} not found");
            }

            // Validate referential integrity - check that WorkflowId and AssignmentIds exist
            _logger.LogInformationWithCorrelation("Validating referential integrity for OrchestratedFlow update. WorkflowId: {WorkflowId}, AssignmentIds: {AssignmentIds}, User: {User}, RequestId: {RequestId}",
                entity.WorkflowId, string.Join(",", entity.AssignmentIds), userContext, HttpContext.TraceIdentifier);

            var workflowExists = await _workflowValidationService.ValidateWorkflowExistsAsync(entity.WorkflowId);
            if (!workflowExists)
            {
                _logger.LogWarningWithCorrelation("Cannot update OrchestratedFlow - referenced workflow does not exist. WorkflowId: {WorkflowId}, User: {User}, RequestId: {RequestId}",
                    entity.WorkflowId, userContext, HttpContext.TraceIdentifier);
                return BadRequest($"Referenced workflow with ID {entity.WorkflowId} does not exist");
            }

            var assignmentsExist = await _assignmentValidationService.ValidateAssignmentsExistAsync(entity.AssignmentIds);
            if (!assignmentsExist)
            {
                _logger.LogWarningWithCorrelation("Cannot update OrchestratedFlow - one or more referenced assignments do not exist. AssignmentIds: {AssignmentIds}, User: {User}, RequestId: {RequestId}",
                    string.Join(",", entity.AssignmentIds), userContext, HttpContext.TraceIdentifier);
                return BadRequest("One or more referenced assignments do not exist");
            }

            entity.UpdatedBy = userContext;
            entity.CreatedAt = existingEntity.CreatedAt;
            entity.CreatedBy = existingEntity.CreatedBy;

            _logger.LogDebugWithCorrelation("Updating OrchestratedFlow entity with details. Id: {Id}, Version: {Version}, Name: {Name}, WorkflowId: {WorkflowId}, AssignmentIds: {AssignmentIds}, UpdatedBy: {UpdatedBy}, User: {User}, RequestId: {RequestId}",
                entity.Id, entity.Version, entity.Name, entity.WorkflowId, string.Join(",", entity.AssignmentIds), entity.UpdatedBy, userContext, HttpContext.TraceIdentifier);

            var updated = await _repository.UpdateAsync(entity);

            _logger.LogInformationWithCorrelation("Successfully updated OrchestratedFlow entity. Id: {Id}, Version: {Version}, Name: {Name}, WorkflowId: {WorkflowId}, AssignmentIds: {AssignmentIds}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                updated.Id, updated.Version, updated.Name, updated.WorkflowId, string.Join(",", updated.AssignmentIds), updated.GetCompositeKey(), userContext, HttpContext.TraceIdentifier);

            return Ok(updated);
        }
        catch (DuplicateKeyException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Duplicate key conflict updating OrchestratedFlow entity. Id: {Id}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error updating OrchestratedFlow entity. Id: {Id}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while updating the OrchestratedFlow entity");
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

        _logger.LogInformationWithCorrelation("Starting Delete OrchestratedFlow request. Id: {Id}, User: {User}, RequestId: {RequestId}",
            guidId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(guidId);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("OrchestratedFlow entity not found for deletion. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"OrchestratedFlow entity with ID {guidId} not found");
            }

            _logger.LogDebugWithCorrelation("Deleting OrchestratedFlow entity. Id: {Id}, Version: {Version}, Name: {Name}, WorkflowId: {WorkflowId}, AssignmentIds: {AssignmentIds}, User: {User}, RequestId: {RequestId}",
                guidId, existingEntity.Version, existingEntity.Name, existingEntity.WorkflowId, string.Join(",", existingEntity.AssignmentIds), userContext, HttpContext.TraceIdentifier);

            var success = await _repository.DeleteAsync(guidId);

            if (!success)
            {
                _logger.LogErrorWithCorrelation("Failed to delete OrchestratedFlow entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return StatusCode(500, "Failed to delete OrchestratedFlow entity");
            }

            _logger.LogInformationWithCorrelation("Successfully deleted OrchestratedFlow entity. Id: {Id}, Version: {Version}, Name: {Name}, WorkflowId: {WorkflowId}, AssignmentIds: {AssignmentIds}, User: {User}, RequestId: {RequestId}",
                guidId, existingEntity.Version, existingEntity.Name, existingEntity.WorkflowId, string.Join(",", existingEntity.AssignmentIds), userContext, HttpContext.TraceIdentifier);

            return NoContent();
        }
        
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error deleting OrchestratedFlow entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while deleting the OrchestratedFlow entity");
        }
    }

    [HttpGet("composite")]
    public ActionResult<OrchestratedFlowEntity> GetByCompositeKeyEmpty()
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
