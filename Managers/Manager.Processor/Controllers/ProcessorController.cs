using Manager.Processor.Repositories;
using Manager.Processor.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Entities;
using Shared.Exceptions;
using Shared.Correlation;
using Shared.Services;
using Swashbuckle.AspNetCore.Annotations;
using System.Diagnostics;

namespace Manager.Processor.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Processor management operations")]
public class ProcessorController : ControllerBase
{
    private readonly IProcessorEntityRepository _repository;
    private readonly IEntityReferenceValidator _entityReferenceValidator;
    private readonly ISchemaValidationService _schemaValidator;
    private readonly ILogger<ProcessorController> _logger;

    public ProcessorController(
        IProcessorEntityRepository repository,
        IEntityReferenceValidator entityReferenceValidator,
        ISchemaValidationService schemaValidator,
        ILogger<ProcessorController> logger)
    {
        _repository = repository;
        _entityReferenceValidator = entityReferenceValidator;
        _schemaValidator = schemaValidator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProcessorEntity>>> GetAll()
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetAll schemas request. User: {User}, RequestId: {RequestId}",
            userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetAllAsync();
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Processor entities. User: {User}, RequestId: {RequestId}",
                count, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving all Processor entities. User: {User}, RequestId: {RequestId}",
                userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Processor entities");
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

            _logger.LogInformationWithCorrelation("Successfully retrieved paged Processor entities. Page: {Page}/{TotalPages}, PageSize: {PageSize}, TotalCount: {TotalCount}, User: {User}, RequestId: {RequestId}",
                page, totalPages, pageSize, totalCount, userContext, HttpContext.TraceIdentifier);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving paged Processor entities. Page: {Page}, PageSize: {PageSize}, User: {User}, RequestId: {RequestId}",
                originalPage, originalPageSize, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving paged Processor entities");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProcessorEntity>> GetById(string id)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(id, out Guid guidId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided. Id: {Id}, User: {User}, RequestId: {RequestId}",
                id, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {id}");
        }

        _logger.LogInformationWithCorrelation("Starting GetById Processor request. Id: {Id}, User: {User}, RequestId: {RequestId}",
            guidId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entity = await _repository.GetByIdAsync(guidId);

            if (entity == null)
            {
                _logger.LogWarningWithCorrelation("Processor entity not found. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Processor entity with ID {guidId} not found");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved Processor entity. Id: {Id}, Version: {Version}, Name: {Name}, InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}, User: {User}, RequestId: {RequestId}",
                entity.Id, entity.Version, entity.Name, entity.InputSchemaId, entity.OutputSchemaId, userContext, HttpContext.TraceIdentifier);

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Processor entity by ID. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving the Processor entity");
        }
    }

    [HttpGet("composite/{version}/{name}")]
    public async Task<ActionResult<ProcessorEntity>> GetByCompositeKey(string version, string name)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        // ProcessorEntity composite key format: "version_name"
        var compositeKey = $"{version}_{name}";

        _logger.LogInformationWithCorrelation("Starting GetByCompositeKey Processor request. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
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
                _logger.LogWarningWithCorrelation("Processor entity not found by composite key. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                    version, name, compositeKey, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Processor entity with version '{version}' and name '{name}' not found");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved Processor entity by composite key. CompositeKey: {CompositeKey}, Id: {Id}, Version: {Version}, Name: {Name}, InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}, User: {User}, RequestId: {RequestId}",
                compositeKey, entity.Id, entity.Version, entity.Name, entity.InputSchemaId, entity.OutputSchemaId, userContext, HttpContext.TraceIdentifier);

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Processor entity by composite key. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                version, name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving the Processor entity");
        }
    }

    [HttpGet("input-schema/{inputSchemaId}")]
    public async Task<ActionResult<IEnumerable<ProcessorEntity>>> GetByInputSchemaId(string inputSchemaId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(inputSchemaId, out Guid guidInputSchemaId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided for input schema ID. InputSchemaId: {InputSchemaId}, User: {User}, RequestId: {RequestId}",
                inputSchemaId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {inputSchemaId}");
        }

        _logger.LogInformationWithCorrelation("Starting GetByInputSchemaId Processor request. InputSchemaId: {InputSchemaId}, User: {User}, RequestId: {RequestId}",
            guidInputSchemaId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetByInputSchemaIdAsync(guidInputSchemaId);
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Processor entities by input schema ID. InputSchemaId: {InputSchemaId}, User: {User}, RequestId: {RequestId}",
                count, guidInputSchemaId, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Processor entities by input schema ID. InputSchemaId: {InputSchemaId}, User: {User}, RequestId: {RequestId}",
                guidInputSchemaId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Processor entities");
        }
    }

    [HttpGet("output-schema/{outputSchemaId}")]
    public async Task<ActionResult<IEnumerable<ProcessorEntity>>> GetByOutputSchemaId(string outputSchemaId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(outputSchemaId, out Guid guidOutputSchemaId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided for output schema ID. OutputSchemaId: {OutputSchemaId}, User: {User}, RequestId: {RequestId}",
                outputSchemaId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {outputSchemaId}");
        }

        _logger.LogInformationWithCorrelation("Starting GetByOutputSchemaId Processor request. OutputSchemaId: {OutputSchemaId}, User: {User}, RequestId: {RequestId}",
            guidOutputSchemaId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetByOutputSchemaIdAsync(guidOutputSchemaId);
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Processor entities by output schema ID. OutputSchemaId: {OutputSchemaId}, User: {User}, RequestId: {RequestId}",
                count, guidOutputSchemaId, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Processor entities by output schema ID. OutputSchemaId: {OutputSchemaId}, User: {User}, RequestId: {RequestId}",
                guidOutputSchemaId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Processor entities");
        }
    }

    [HttpGet("version/{version}")]
    public async Task<ActionResult<IEnumerable<ProcessorEntity>>> GetByVersion(string version)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByVersion Processor request. Version: {Version}, User: {User}, RequestId: {RequestId}",
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

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Processor entities by version. Version: {Version}, User: {User}, RequestId: {RequestId}",
                count, version, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Processor entities by version. Version: {Version}, User: {User}, RequestId: {RequestId}",
                version, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Processor entities");
        }
    }

    [HttpGet("name/{name}")]
    public async Task<ActionResult<IEnumerable<ProcessorEntity>>> GetByName(string name)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByName Processor request. Name: {Name}, User: {User}, RequestId: {RequestId}",
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

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Processor entities by name. Name: {Name}, User: {User}, RequestId: {RequestId}",
                count, name, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Processor entities by name. Name: {Name}, User: {User}, RequestId: {RequestId}",
                name, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Processor entities");
        }
    }

    [HttpPost]
    public async Task<ActionResult<ProcessorEntity>> Create([FromBody] ProcessorEntity entity)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        var compositeKey = entity?.GetCompositeKey() ?? "Unknown";

        _logger.LogInformationWithCorrelation("Starting Create Processor request. CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            compositeKey, userContext, HttpContext.TraceIdentifier);

        if (entity == null)
        {
            _logger.LogWarningWithCorrelation("Null entity provided for Processor creation. User: {User}, RequestId: {RequestId}",
                userContext, HttpContext.TraceIdentifier);
            return BadRequest("Processor entity cannot be null");
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarningWithCorrelation("Invalid model state for Processor creation. Errors: {Errors}, User: {User}, RequestId: {RequestId}",
                errorMessage, userContext, HttpContext.TraceIdentifier);
            return BadRequest(ModelState);
        }

        try
        {
            entity!.CreatedBy = userContext;
            entity.Id = Guid.Empty;

            // Validate schemas exist before creating the entity
            _logger.LogDebugWithCorrelation("Validating schema references before creating Processor entity. InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}, User: {User}, RequestId: {RequestId}",
                entity.InputSchemaId, entity.OutputSchemaId, userContext, HttpContext.TraceIdentifier);

            await _schemaValidator.ValidateSchemasExist(entity.InputSchemaId, entity.OutputSchemaId);

            _logger.LogDebugWithCorrelation("Schema validation passed. Creating Processor entity with details. Version: {Version}, Name: {Name}, InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}, CreatedBy: {CreatedBy}, User: {User}, RequestId: {RequestId}",
                entity.Version, entity.Name, entity.InputSchemaId, entity.OutputSchemaId, entity.CreatedBy, userContext, HttpContext.TraceIdentifier);

            var created = await _repository.CreateAsync(entity);

            if (created.Id == Guid.Empty)
            {
                _logger.LogErrorWithCorrelation("MongoDB failed to generate ID for new ProcessorEntity. Version: {Version}, Name: {Name}, User: {User}, RequestId: {RequestId}",
                    entity.Version, entity.Name, userContext, HttpContext.TraceIdentifier);
                return StatusCode(500, "Failed to generate entity ID");
            }

            _logger.LogInformationWithCorrelation("Successfully created Processor entity. Id: {Id}, Version: {Version}, Name: {Name}, InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                created.Id, created.Version, created.Name, created.InputSchemaId, created.OutputSchemaId, created.GetCompositeKey(), userContext, HttpContext.TraceIdentifier);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Schema") || ex.Message.Contains("schema"))
        {
            _logger.LogWarningWithCorrelation(ex, "Schema validation failed creating Processor entity. InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.InputSchemaId, entity?.OutputSchemaId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return BadRequest(new {
                error = "Schema validation failed",
                message = ex.Message,
                inputSchemaId = entity?.InputSchemaId,
                outputSchemaId = entity?.OutputSchemaId
            });
        }
        catch (DuplicateKeyException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Duplicate key conflict creating Processor entity. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error creating Processor entity. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while creating the Processor entity");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProcessorEntity>> Update(string id, [FromBody] ProcessorEntity entity)
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

        _logger.LogInformationWithCorrelation("Starting Update Processor request. Id: {Id}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            guidId, compositeKey, userContext, HttpContext.TraceIdentifier);

        if (entity == null)
        {
            _logger.LogWarningWithCorrelation("Null entity provided for Processor update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return BadRequest("Processor entity cannot be null");
        }

        if (guidId != entity.Id)
        {
            _logger.LogWarningWithCorrelation("ID mismatch in Processor update request. URL ID: {UrlId}, Entity ID: {EntityId}, User: {User}, RequestId: {RequestId}",
                guidId, entity.Id, userContext, HttpContext.TraceIdentifier);
            return BadRequest("ID in URL does not match entity ID");
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarningWithCorrelation("Invalid model state for Processor update. Id: {Id}, Errors: {Errors}, User: {User}, RequestId: {RequestId}",
                guidId, errorMessage, userContext, HttpContext.TraceIdentifier);
            return BadRequest(ModelState);
        }

        try
        {
            var existingEntity = await _repository.GetByIdAsync(guidId);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Processor entity not found for update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Processor entity with ID {guidId} not found");
            }

            // Validate entity can be updated (check for references if critical properties changed)
            try
            {
                await _entityReferenceValidator.ValidateProcessorCanBeUpdated(guidId, existingEntity, entity);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("referenced by") || ex.Message.Contains("cannot be changed"))
            {
                _logger.LogWarningWithCorrelation("Processor update blocked due to step references. Id: {Id}, Message: {Message}, User: {User}, RequestId: {RequestId}",
                    guidId, ex.Message, userContext, HttpContext.TraceIdentifier);
                return Conflict(new
                {
                    error = "Processor has references",
                    message = ex.Message,
                    processorId = guidId,
                    version = existingEntity.Version,
                    name = existingEntity.Name
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("validation"))
            {
                _logger.LogErrorWithCorrelation(ex, "Processor reference validation failed during update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return StatusCode(503, new
                {
                    error = "Validation service unavailable",
                    message = "Cannot validate processor references - validation service is unavailable. Operation rejected for safety.",
                    details = ex.Message
                });
            }

            // Validate schemas exist before updating the entity
            _logger.LogDebugWithCorrelation("Validating schema references before updating Processor entity. InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}, User: {User}, RequestId: {RequestId}",
                entity.InputSchemaId, entity.OutputSchemaId, userContext, HttpContext.TraceIdentifier);

            await _schemaValidator.ValidateSchemasExist(entity.InputSchemaId, entity.OutputSchemaId);

            entity.UpdatedBy = userContext;
            entity.CreatedAt = existingEntity.CreatedAt;
            entity.CreatedBy = existingEntity.CreatedBy;

            _logger.LogDebugWithCorrelation("Schema validation passed. Updating Processor entity with details. Id: {Id}, Version: {Version}, Name: {Name}, InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}, UpdatedBy: {UpdatedBy}, User: {User}, RequestId: {RequestId}",
                entity.Id, entity.Version, entity.Name, entity.InputSchemaId, entity.OutputSchemaId, entity.UpdatedBy, userContext, HttpContext.TraceIdentifier);

            var updated = await _repository.UpdateAsync(entity);

            _logger.LogInformationWithCorrelation("Successfully updated Processor entity. Id: {Id}, Version: {Version}, Name: {Name}, InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                updated.Id, updated.Version, updated.Name, updated.InputSchemaId, updated.OutputSchemaId, updated.GetCompositeKey(), userContext, HttpContext.TraceIdentifier);

            return Ok(updated);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Schema") || ex.Message.Contains("schema"))
        {
            _logger.LogWarningWithCorrelation(ex, "Schema validation failed updating Processor entity. Id: {Id}, InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.InputSchemaId, entity?.OutputSchemaId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return BadRequest(new {
                error = "Schema validation failed",
                message = ex.Message,
                inputSchemaId = entity?.InputSchemaId,
                outputSchemaId = entity?.OutputSchemaId
            });
        }
        catch (DuplicateKeyException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Duplicate key conflict updating Processor entity. Id: {Id}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error updating Processor entity. Id: {Id}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while updating the Processor entity");
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

        _logger.LogInformationWithCorrelation("Starting Delete Processor request. Id: {Id}, User: {User}, RequestId: {RequestId}",
            guidId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(guidId);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Processor entity not found for deletion. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Processor entity with ID {guidId} not found");
            }

            // Validate entity can be deleted (check for references in Step entities)
            try
            {
                await _entityReferenceValidator.ValidateProcessorCanBeDeleted(guidId);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("referenced by"))
            {
                _logger.LogWarningWithCorrelation("Processor deletion blocked due to step references. Id: {Id}, Message: {Message}, User: {User}, RequestId: {RequestId}",
                    guidId, ex.Message, userContext, HttpContext.TraceIdentifier);
                return Conflict(new
                {
                    error = "Processor has references",
                    message = ex.Message,
                    processorId = guidId,
                    version = existingEntity.Version,
                    name = existingEntity.Name
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("validation"))
            {
                _logger.LogErrorWithCorrelation(ex, "Processor reference validation failed during deletion. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return StatusCode(503, new
                {
                    error = "Validation service unavailable",
                    message = "Cannot validate processor references - validation service is unavailable. Operation rejected for safety.",
                    details = ex.Message
                });
            }

            _logger.LogDebugWithCorrelation("Deleting Processor entity. Id: {Id}, Version: {Version}, Name: {Name}, InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}, User: {User}, RequestId: {RequestId}",
                guidId, existingEntity.Version, existingEntity.Name, existingEntity.InputSchemaId, existingEntity.OutputSchemaId, userContext, HttpContext.TraceIdentifier);

            var success = await _repository.DeleteAsync(guidId);

            if (!success)
            {
                _logger.LogErrorWithCorrelation("Failed to delete Processor entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return StatusCode(500, "Failed to delete Processor entity");
            }

            _logger.LogInformationWithCorrelation("Successfully deleted Processor entity. Id: {Id}, Version: {Version}, Name: {Name}, InputSchemaId: {InputSchemaId}, OutputSchemaId: {OutputSchemaId}, User: {User}, RequestId: {RequestId}",
                guidId, existingEntity.Version, existingEntity.Name, existingEntity.InputSchemaId, existingEntity.OutputSchemaId, userContext, HttpContext.TraceIdentifier);

            return NoContent();
        }
        
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error deleting Processor entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while deleting the Processor entity");
        }
    }

    [HttpGet("composite")]
    public ActionResult<ProcessorEntity> GetByCompositeKeyEmpty()
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

    /// <summary>
    /// Check if a schema is referenced as input schema by any processor entities
    /// </summary>
    /// <param name="schemaId">The schema ID to check</param>
    /// <returns>True if schema is referenced as input schema, false otherwise</returns>
    [HttpGet("input-schema/{schemaId}/exists")]
    public async Task<ActionResult<bool>> CheckInputSchemaReference(Guid schemaId)
    {
        var requestId = HttpContext.TraceIdentifier;
        var user = HttpContext.User?.Identity?.Name ?? "Anonymous";

        _logger.LogInformationWithCorrelation("Starting input schema reference check. SchemaId: {SchemaId}, User: {User}, RequestId: {RequestId}",
            schemaId, user, requestId);

        try
        {
            if (schemaId == Guid.Empty)
            {
                _logger.LogWarningWithCorrelation("Invalid schema ID provided for input schema reference check. SchemaId: {SchemaId}, User: {User}, RequestId: {RequestId}",
                    schemaId, user, requestId);
                return BadRequest("Schema ID cannot be empty");
            }

            var hasReferences = await _repository.HasInputSchemaReferences(schemaId);

            _logger.LogInformationWithCorrelation("Input schema reference check completed. SchemaId: {SchemaId}, HasReferences: {HasReferences}, User: {User}, RequestId: {RequestId}",
                schemaId, hasReferences, user, requestId);

            return Ok(hasReferences);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error checking input schema references. SchemaId: {SchemaId}, User: {User}, RequestId: {RequestId}",
                schemaId, user, requestId);
            return StatusCode(500, "Internal server error while checking input schema references");
        }
    }

    /// <summary>
    /// Check if a schema is referenced as output schema by any processor entities
    /// </summary>
    /// <param name="schemaId">The schema ID to check</param>
    /// <returns>True if schema is referenced as output schema, false otherwise</returns>
    [HttpGet("output-schema/{schemaId}/exists")]
    public async Task<ActionResult<bool>> CheckOutputSchemaReference(Guid schemaId)
    {
        var requestId = HttpContext.TraceIdentifier;
        var user = HttpContext.User?.Identity?.Name ?? "Anonymous";

        _logger.LogInformationWithCorrelation("Starting output schema reference check. SchemaId: {SchemaId}, User: {User}, RequestId: {RequestId}",
            schemaId, user, requestId);

        try
        {
            if (schemaId == Guid.Empty)
            {
                _logger.LogWarningWithCorrelation("Invalid schema ID provided for output schema reference check. SchemaId: {SchemaId}, User: {User}, RequestId: {RequestId}",
                    schemaId, user, requestId);
                return BadRequest("Schema ID cannot be empty");
            }

            var hasReferences = await _repository.HasOutputSchemaReferences(schemaId);

            _logger.LogInformationWithCorrelation("Output schema reference check completed. SchemaId: {SchemaId}, HasReferences: {HasReferences}, User: {User}, RequestId: {RequestId}",
                schemaId, hasReferences, user, requestId);

            return Ok(hasReferences);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error checking output schema references. SchemaId: {SchemaId}, User: {User}, RequestId: {RequestId}",
                schemaId, user, requestId);
            return StatusCode(500, "Internal server error while checking output schema references");
        }
    }

    /// <summary>
    /// Check if a processor exists (used for referential integrity validation)
    /// </summary>
    /// <param name="processorId">The processor ID to check</param>
    /// <returns>True if processor exists, false otherwise</returns>
    [HttpGet("{processorId}/exists")]
    public async Task<ActionResult<bool>> CheckProcessorExists(string processorId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(processorId, out var guidProcessorId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format for ProcessorId in exists check. ProcessorId: {ProcessorId}, User: {User}, RequestId: {RequestId}",
                processorId, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {processorId}");
        }

        _logger.LogInformationWithCorrelation("Starting CheckProcessorExists request. ProcessorId: {ProcessorId}, User: {User}, RequestId: {RequestId}",
            guidProcessorId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entity = await _repository.GetByIdAsync(guidProcessorId);
            var exists = entity != null;

            _logger.LogInformationWithCorrelation("Successfully processed CheckProcessorExists request. ProcessorId: {ProcessorId}, Exists: {Exists}, User: {User}, RequestId: {RequestId}",
                guidProcessorId, exists, userContext, HttpContext.TraceIdentifier);

            return Ok(exists);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error processing CheckProcessorExists request. ProcessorId: {ProcessorId}, User: {User}, RequestId: {RequestId}",
                guidProcessorId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while processing the request");
        }
    }
}
