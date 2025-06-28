using Manager.Step.Repositories;
using Manager.Step.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Correlation;
using Shared.Entities;
using Shared.Exceptions;
using Swashbuckle.AspNetCore.Annotations;

namespace Manager.Step.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Step management operations")]
public class StepController : ControllerBase
{
    private readonly IStepEntityRepository _repository;
    private readonly IEntityReferenceValidator _entityReferenceValidator;
    private readonly IProcessorValidationService _processorValidator;
    private readonly ILogger<StepController> _logger;

    public StepController(
        IStepEntityRepository repository,
        IEntityReferenceValidator entityReferenceValidator,
        IProcessorValidationService processorValidator,
        ILogger<StepController> logger)
    {
        _repository = repository;
        _entityReferenceValidator = entityReferenceValidator;
        _processorValidator = processorValidator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StepEntity>>> GetAll()
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetAll schemas request. User: {User}, RequestId: {RequestId}",
            userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetAllAsync();
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Step entities. User: {User}, RequestId: {RequestId}",
                count, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving all Step entities. User: {User}, RequestId: {RequestId}",
                userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Step entities");
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

            _logger.LogInformationWithCorrelation("Successfully retrieved paged Step entities. Page: {Page}/{TotalPages}, PageSize: {PageSize}, TotalCount: {TotalCount}, User: {User}, RequestId: {RequestId}",
                page, totalPages, pageSize, totalCount, userContext, HttpContext.TraceIdentifier);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving paged Step entities. Page: {Page}, PageSize: {PageSize}, User: {User}, RequestId: {RequestId}",
                originalPage, originalPageSize, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving paged Step entities");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StepEntity>> GetById(string id)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(id, out Guid guidId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided. Id: {Id}, User: {User}, RequestId: {RequestId}",
                id, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {id}");
        }

        _logger.LogInformationWithCorrelation("Starting GetById Step request. Id: {Id}, User: {User}, RequestId: {RequestId}",
            guidId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entity = await _repository.GetByIdAsync(guidId);

            if (entity == null)
            {
                _logger.LogWarningWithCorrelation("Step entity not found. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Step entity with ID {guidId} not found");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved Step entity. Id: {Id}, Version: {Version}, Name: {Name}, ProcessorId: {ProcessorId}, NextStepIds: {NextStepIds}, EntryCondition: {EntryCondition}, User: {User}, RequestId: {RequestId}",
                entity.Id, entity.Version, entity.Name, entity.ProcessorId, string.Join(",", entity.NextStepIds), entity.EntryCondition, userContext, HttpContext.TraceIdentifier);

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Step entity by ID. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving the Step entity");
        }
    }

    [HttpGet("composite/{version}/{name}")]
    public async Task<ActionResult<StepEntity>> GetByCompositeKey(string version, string name)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        // StepEntity composite key format: "version_name"
        var compositeKey = $"{version}_{name}";

        _logger.LogInformationWithCorrelation("Starting GetByCompositeKey Step request. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
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
                _logger.LogWarningWithCorrelation("Step entity not found by composite key. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                    version, name, compositeKey, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Step entity with version '{version}' and name '{name}' not found");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved Step entity by composite key. CompositeKey: {CompositeKey}, Id: {Id}, Version: {Version}, Name: {Name}, ProcessorId: {ProcessorId}, NextStepIds: {NextStepIds}, EntryCondition: {EntryCondition}, User: {User}, RequestId: {RequestId}",
                compositeKey, entity.Id, entity.Version, entity.Name, entity.ProcessorId, string.Join(",", entity.NextStepIds), entity.EntryCondition, userContext, HttpContext.TraceIdentifier);

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Step entity by composite key. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                version, name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving the Step entity");
        }
    }

    [HttpGet("processor/{processorId}")]
    public async Task<ActionResult<IEnumerable<StepEntity>>> GetByProcessorId(string processorId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(processorId, out var guidProcessorId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format for ProcessorId. ProcessorId: {ProcessorId}, User: {User}, RequestId: {RequestId}",
                processorId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {processorId}");
        }

        _logger.LogInformationWithCorrelation("Starting GetByProcessorId Step request. ProcessorId: {ProcessorId}, User: {User}, RequestId: {RequestId}",
            guidProcessorId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetByProcessorIdAsync(guidProcessorId);
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Step entities by ProcessorId. ProcessorId: {ProcessorId}, User: {User}, RequestId: {RequestId}",
                count, guidProcessorId, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Step entities by ProcessorId. ProcessorId: {ProcessorId}, User: {User}, RequestId: {RequestId}",
                guidProcessorId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Step entities");
        }
    }

    [HttpGet("processor/{processorId}/exists")]
    public async Task<ActionResult<bool>> CheckProcessorReferences(string processorId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(processorId, out var guidProcessorId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format for ProcessorId in reference check. ProcessorId: {ProcessorId}, User: {User}, RequestId: {RequestId}",
                processorId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {processorId}");
        }

        _logger.LogInformationWithCorrelation("Starting CheckProcessorReferences request. ProcessorId: {ProcessorId}, User: {User}, RequestId: {RequestId}",
            guidProcessorId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetByProcessorIdAsync(guidProcessorId);
            var hasReferences = entities.Any();

            _logger.LogInformationWithCorrelation("Successfully processed CheckProcessorReferences request. ProcessorId: {ProcessorId}, HasReferences: {HasReferences}, Count: {Count}, User: {User}, RequestId: {RequestId}",
                guidProcessorId, hasReferences, entities.Count(), userContext, HttpContext.TraceIdentifier);

            return Ok(hasReferences);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error processing CheckProcessorReferences request. ProcessorId: {ProcessorId}, User: {User}, RequestId: {RequestId}",
                guidProcessorId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while processing the request");
        }
    }

    [HttpGet("nextstep/{stepId}")]
    public async Task<ActionResult<IEnumerable<StepEntity>>> GetByNextStepId(string stepId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(stepId, out var guidStepId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format for StepId. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
                stepId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {stepId}");
        }

        _logger.LogInformationWithCorrelation("Starting GetByNextStepId Step request. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
            guidStepId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetByNextStepIdAsync(guidStepId);
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Step entities that reference StepId. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
                count, guidStepId, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Step entities by NextStepId. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
                guidStepId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Step entities");
        }
    }

    [HttpGet("entrycondition/{condition}")]
    public async Task<ActionResult<IEnumerable<StepEntity>>> GetByEntryCondition(string condition)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate enum format
        if (!Enum.TryParse<Shared.Entities.Enums.StepEntryCondition>(condition, true, out var entryCondition))
        {
            _logger.LogWarningWithCorrelation("Invalid EntryCondition format. Condition: {Condition}, User: {User}, RequestId: {RequestId}",
                condition, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid EntryCondition: {condition}. Valid values: {string.Join(", ", Enum.GetNames<Shared.Entities.Enums.StepEntryCondition>())}");
        }

        _logger.LogInformationWithCorrelation("Starting GetByEntryCondition Step request. EntryCondition: {EntryCondition}, User: {User}, RequestId: {RequestId}",
            entryCondition, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetByEntryConditionAsync(entryCondition);
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Step entities by EntryCondition. EntryCondition: {EntryCondition}, User: {User}, RequestId: {RequestId}",
                count, entryCondition, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Step entities by EntryCondition. EntryCondition: {EntryCondition}, User: {User}, RequestId: {RequestId}",
                entryCondition, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Step entities");
        }
    }

    [HttpGet("{stepId}/exists")]
    public async Task<ActionResult<bool>> CheckStepExists(string stepId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(stepId, out var guidStepId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format for StepId in exists check. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
                stepId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {stepId}");
        }

        _logger.LogInformationWithCorrelation("Starting CheckStepExists request. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
            guidStepId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var exists = await _repository.ExistsAsync(guidStepId);

            _logger.LogInformationWithCorrelation("Successfully processed CheckStepExists request. StepId: {StepId}, Exists: {Exists}, User: {User}, RequestId: {RequestId}",
                guidStepId, exists, userContext, HttpContext.TraceIdentifier);

            return Ok(exists);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error processing CheckStepExists request. StepId: {StepId}, User: {User}, RequestId: {RequestId}",
                guidStepId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while processing the request");
        }
    }

    [HttpGet("version/{version}")]
    public async Task<ActionResult<IEnumerable<StepEntity>>> GetByVersion(string version)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByVersion Step request. Version: {Version}, User: {User}, RequestId: {RequestId}",
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

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Step entities by version. Version: {Version}, User: {User}, RequestId: {RequestId}",
                count, version, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Step entities by version. Version: {Version}, User: {User}, RequestId: {RequestId}",
                version, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Step entities");
        }
    }

    [HttpGet("name/{name}")]
    public async Task<ActionResult<IEnumerable<StepEntity>>> GetByName(string name)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByName Step request. Name: {Name}, User: {User}, RequestId: {RequestId}",
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

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Step entities by name. Name: {Name}, User: {User}, RequestId: {RequestId}",
                count, name, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Step entities by name. Name: {Name}, User: {User}, RequestId: {RequestId}",
                name, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Step entities");
        }
    }

    [HttpPost]
    public async Task<ActionResult<StepEntity>> Create([FromBody] StepEntity entity)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        var compositeKey = entity?.GetCompositeKey() ?? "Unknown";

        _logger.LogInformationWithCorrelation("Starting Create Step request. CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            compositeKey, userContext, HttpContext.TraceIdentifier);

        if (entity == null)
        {
            _logger.LogWarningWithCorrelation("Null entity provided for Step creation. User: {User}, RequestId: {RequestId}",
                userContext, HttpContext.TraceIdentifier);
            return BadRequest("Step entity cannot be null");
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarningWithCorrelation("Invalid model state for Step creation. Errors: {Errors}, User: {User}, RequestId: {RequestId}",
                errorMessage, userContext, HttpContext.TraceIdentifier);
            return BadRequest(ModelState);
        }

        try
        {
            entity!.CreatedBy = userContext;
            entity.Id = Guid.Empty;

            // Validate processor exists before creating the entity
            _logger.LogDebugWithCorrelation("Validating processor reference before creating Step entity. ProcessorId: {ProcessorId}, User: {User}, RequestId: {RequestId}",
                entity.ProcessorId, userContext, HttpContext.TraceIdentifier);

            await _processorValidator.ValidateProcessorExists(entity.ProcessorId);

            // Validate next step IDs exist before creating the entity
            _logger.LogDebugWithCorrelation("Validating next step references before creating Step entity. NextStepIds: {NextStepIds}, User: {User}, RequestId: {RequestId}",
                string.Join(",", entity.NextStepIds), userContext, HttpContext.TraceIdentifier);

            await _processorValidator.ValidateNextStepsExist(entity.NextStepIds);

            _logger.LogDebugWithCorrelation("All validations passed. Creating Step entity with details. Version: {Version}, Name: {Name}, ProcessorId: {ProcessorId}, NextStepIds: {NextStepIds}, EntryCondition: {EntryCondition}, CreatedBy: {CreatedBy}, User: {User}, RequestId: {RequestId}",
                entity.Version, entity.Name, entity.ProcessorId, string.Join(",", entity.NextStepIds), entity.EntryCondition, entity.CreatedBy, userContext, HttpContext.TraceIdentifier);

            var created = await _repository.CreateAsync(entity);

            if (created.Id == Guid.Empty)
            {
                _logger.LogErrorWithCorrelation("MongoDB failed to generate ID for new StepEntity. Version: {Version}, Name: {Name}, User: {User}, RequestId: {RequestId}",
                    entity.Version, entity.Name, userContext, HttpContext.TraceIdentifier);
                return StatusCode(500, "Failed to generate entity ID");
            }

            _logger.LogInformationWithCorrelation("Successfully created Step entity. Id: {Id}, Version: {Version}, Name: {Name}, ProcessorId: {ProcessorId}, NextStepIds: {NextStepIds}, EntryCondition: {EntryCondition}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                created.Id, created.Version, created.Name, created.ProcessorId, string.Join(",", created.NextStepIds), created.EntryCondition, created.GetCompositeKey(), userContext, HttpContext.TraceIdentifier);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Processor") || ex.Message.Contains("processor"))
        {
            _logger.LogWarningWithCorrelation(ex, "Processor validation failed creating Step entity. ProcessorId: {ProcessorId}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.ProcessorId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return BadRequest(new {
                error = "Processor validation failed",
                message = ex.Message,
                processorId = entity?.ProcessorId
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("step") && (ex.Message.Contains("do not exist") || ex.Message.Contains("validation service")))
        {
            _logger.LogWarningWithCorrelation(ex, "Next step validation failed creating Step entity. NextStepIds: {NextStepIds}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                string.Join(",", entity?.NextStepIds ?? new List<Guid>()), entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return BadRequest(new {
                error = "Next step validation failed",
                message = ex.Message,
                nextStepIds = entity?.NextStepIds
            });
        }
        catch (DuplicateKeyException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Duplicate key conflict creating Step entity. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error creating Step entity. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while creating the Step entity");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<StepEntity>> Update(string id, [FromBody] StepEntity entity)
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

        _logger.LogInformationWithCorrelation("Starting Update Step request. Id: {Id}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            guidId, compositeKey, userContext, HttpContext.TraceIdentifier);

        if (entity == null)
        {
            _logger.LogWarningWithCorrelation("Null entity provided for Step update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return BadRequest("Step entity cannot be null");
        }

        if (guidId != entity.Id)
        {
            _logger.LogWarningWithCorrelation("ID mismatch in Step update request. URL ID: {UrlId}, Entity ID: {EntityId}, User: {User}, RequestId: {RequestId}",
                guidId, entity.Id, userContext, HttpContext.TraceIdentifier);
            return BadRequest("ID in URL does not match entity ID");
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarningWithCorrelation("Invalid model state for Step update. Id: {Id}, Errors: {Errors}, User: {User}, RequestId: {RequestId}",
                guidId, errorMessage, userContext, HttpContext.TraceIdentifier);
            return BadRequest(ModelState);
        }

        try
        {
            var existingEntity = await _repository.GetByIdAsync(guidId);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Step entity not found for update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Step entity with ID {guidId} not found");
            }

            // Validate referential integrity before update
            await _entityReferenceValidator.ValidateStepCanBeUpdated(guidId);

            // Validate processor exists before updating the entity
            _logger.LogDebugWithCorrelation("Validating processor reference before updating Step entity. ProcessorId: {ProcessorId}, User: {User}, RequestId: {RequestId}",
                entity.ProcessorId, userContext, HttpContext.TraceIdentifier);

            await _processorValidator.ValidateProcessorExists(entity.ProcessorId);

            // Validate next step IDs exist before updating the entity
            _logger.LogDebugWithCorrelation("Validating next step references before updating Step entity. NextStepIds: {NextStepIds}, User: {User}, RequestId: {RequestId}",
                string.Join(",", entity.NextStepIds), userContext, HttpContext.TraceIdentifier);

            await _processorValidator.ValidateNextStepsExist(entity.NextStepIds);

            entity.UpdatedBy = userContext;
            entity.CreatedAt = existingEntity.CreatedAt;
            entity.CreatedBy = existingEntity.CreatedBy;

            _logger.LogDebugWithCorrelation("All validations passed. Updating Step entity with details. Id: {Id}, Version: {Version}, Name: {Name}, ProcessorId: {ProcessorId}, NextStepIds: {NextStepIds}, EntryCondition: {EntryCondition}, UpdatedBy: {UpdatedBy}, User: {User}, RequestId: {RequestId}",
                entity.Id, entity.Version, entity.Name, entity.ProcessorId, string.Join(",", entity.NextStepIds), entity.EntryCondition, entity.UpdatedBy, userContext, HttpContext.TraceIdentifier);

            var updated = await _repository.UpdateAsync(entity);

            _logger.LogInformationWithCorrelation("Successfully updated Step entity. Id: {Id}, Version: {Version}, Name: {Name}, ProcessorId: {ProcessorId}, NextStepIds: {NextStepIds}, EntryCondition: {EntryCondition}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                updated.Id, updated.Version, updated.Name, updated.ProcessorId, string.Join(",", updated.NextStepIds), updated.EntryCondition, updated.GetCompositeKey(), userContext, HttpContext.TraceIdentifier);

            return Ok(updated);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Processor") || ex.Message.Contains("processor"))
        {
            _logger.LogWarningWithCorrelation(ex, "Processor validation failed updating Step entity. Id: {Id}, ProcessorId: {ProcessorId}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.ProcessorId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return BadRequest(new {
                error = "Processor validation failed",
                message = ex.Message,
                processorId = entity?.ProcessorId
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("step") && (ex.Message.Contains("do not exist") || ex.Message.Contains("validation service")))
        {
            _logger.LogWarningWithCorrelation(ex, "Next step validation failed updating Step entity. Id: {Id}, NextStepIds: {NextStepIds}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, string.Join(",", entity?.NextStepIds ?? new List<Guid>()), entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return BadRequest(new {
                error = "Next step validation failed",
                message = ex.Message,
                nextStepIds = entity?.NextStepIds
            });
        }
        catch (DuplicateKeyException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Duplicate key conflict updating Step entity. Id: {Id}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error updating Step entity. Id: {Id}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while updating the Step entity");
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

        _logger.LogInformationWithCorrelation("Starting Delete Step request. Id: {Id}, User: {User}, RequestId: {RequestId}",
            guidId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(guidId);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Step entity not found for deletion. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Step entity with ID {guidId} not found");
            }

            // Validate referential integrity before deletion
            await _entityReferenceValidator.ValidateStepCanBeDeleted(guidId);

            _logger.LogDebugWithCorrelation("Deleting Step entity. Id: {Id}, Version: {Version}, Name: {Name}, ProcessorId: {ProcessorId}, NextStepIds: {NextStepIds}, EntryCondition: {EntryCondition}, User: {User}, RequestId: {RequestId}",
                guidId, existingEntity.Version, existingEntity.Name, existingEntity.ProcessorId, string.Join(",", existingEntity.NextStepIds), existingEntity.EntryCondition, userContext, HttpContext.TraceIdentifier);

            var success = await _repository.DeleteAsync(guidId);

            if (!success)
            {
                _logger.LogErrorWithCorrelation("Failed to delete Step entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return StatusCode(500, "Failed to delete Step entity");
            }

            _logger.LogInformationWithCorrelation("Successfully deleted Step entity. Id: {Id}, Version: {Version}, Name: {Name}, ProcessorId: {ProcessorId}, NextStepIds: {NextStepIds}, EntryCondition: {EntryCondition}, User: {User}, RequestId: {RequestId}",
                guidId, existingEntity.Version, existingEntity.Name, existingEntity.ProcessorId, string.Join(",", existingEntity.NextStepIds), existingEntity.EntryCondition, userContext, HttpContext.TraceIdentifier);

            return NoContent();
        }
        
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error deleting Step entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while deleting the Step entity");
        }
    }

    [HttpGet("composite")]
    public ActionResult<StepEntity> GetByCompositeKeyEmpty()
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
