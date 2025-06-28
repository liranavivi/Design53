using Manager.Delivery.Repositories;
using Manager.Delivery.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Correlation;
using Shared.Entities;
using Shared.Exceptions;
using Swashbuckle.AspNetCore.Annotations;

namespace Manager.Delivery.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Delivery management operations")]
public class DeliveryController : ControllerBase
{
    private readonly IDeliveryEntityRepository _repository;
    private readonly IEntityReferenceValidator _entityReferenceValidator;
    private readonly ISchemaValidationService _schemaValidator;
    private readonly ILogger<DeliveryController> _logger;

    public DeliveryController(
        IDeliveryEntityRepository repository,
        IEntityReferenceValidator entityReferenceValidator,
        ISchemaValidationService schemaValidator,
        ILogger<DeliveryController> logger)
    {
        _repository = repository;
        _entityReferenceValidator = entityReferenceValidator;
        _schemaValidator = schemaValidator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DeliveryEntity>>> GetAll()
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetAll schemas request. User: {User}, RequestId: {RequestId}",
            userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetAllAsync();
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Delivery entities. User: {User}, RequestId: {RequestId}",
                count, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving all Delivery entities. User: {User}, RequestId: {RequestId}",
                userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Delivery entities");
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

            _logger.LogInformationWithCorrelation("Successfully retrieved paged Delivery entities. Page: {Page}/{TotalPages}, PageSize: {PageSize}, TotalCount: {TotalCount}, User: {User}, RequestId: {RequestId}",
                page, totalPages, pageSize, totalCount, userContext, HttpContext.TraceIdentifier);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving paged Delivery entities. Page: {Page}, PageSize: {PageSize}, User: {User}, RequestId: {RequestId}",
                originalPage, originalPageSize, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving paged Delivery entities");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DeliveryEntity>> GetById(string id)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(id, out Guid guidId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided. Id: {Id}, User: {User}, RequestId: {RequestId}",
                id, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {id}");
        }

        _logger.LogInformationWithCorrelation("Starting GetById Delivery request. Id: {Id}, User: {User}, RequestId: {RequestId}",
            guidId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entity = await _repository.GetByIdAsync(guidId);

            if (entity == null)
            {
                _logger.LogWarningWithCorrelation("Delivery entity not found. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Delivery entity with ID {guidId} not found");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved Delivery entity. Id: {Id}, Version: {Version}, Name: {Name}, Payload: {Payload}, User: {User}, RequestId: {RequestId}",
                entity.Id, entity.Version, entity.Name, entity.Payload, userContext, HttpContext.TraceIdentifier);

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Delivery entity by ID. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving the Delivery entity");
        }
    }

    [HttpGet("composite/{version}/{name}")]
    public async Task<ActionResult<DeliveryEntity>> GetByCompositeKey(string version, string name)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        // DeliveryEntity composite key format: "version_name"
        var compositeKey = $"{version}_{name}";

        _logger.LogInformationWithCorrelation("Starting GetByCompositeKey Delivery request. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
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
                _logger.LogWarningWithCorrelation("Delivery entity not found by composite key. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                    version, name, compositeKey, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Delivery entity with version '{version}' and name '{name}' not found");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved Delivery entity by composite key. CompositeKey: {CompositeKey}, Id: {Id}, Version: {Version}, Name: {Name}, Payload: {Payload}, User: {User}, RequestId: {RequestId}",
                compositeKey, entity.Id, entity.Version, entity.Name, entity.Payload, userContext, HttpContext.TraceIdentifier);

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Delivery entity by composite key. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                version, name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving the Delivery entity");
        }
    }

    [HttpGet("payload/{payload}")]
    public async Task<ActionResult<IEnumerable<DeliveryEntity>>> GetByPayload(string payload)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByPayload Delivery request. Payload: {Payload}, User: {User}, RequestId: {RequestId}",
            payload, userContext, HttpContext.TraceIdentifier);

        try
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                _logger.LogWarningWithCorrelation("Empty or null payload provided. User: {User}, RequestId: {RequestId}",
                    userContext, HttpContext.TraceIdentifier);
                return BadRequest("Payload cannot be empty");
            }

            var entities = await _repository.GetByPayloadAsync(payload);
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Delivery entities by payload. Payload: {Payload}, User: {User}, RequestId: {RequestId}",
                count, payload, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Delivery entities by payload. Payload: {Payload}, User: {User}, RequestId: {RequestId}",
                payload, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Delivery entities");
        }
    }

    [HttpGet("schemaId/{schemaId}")]
    public async Task<ActionResult<IEnumerable<DeliveryEntity>>> GetBySchemaId(Guid schemaId)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetBySchemaId Delivery request. SchemaId: {SchemaId}, User: {User}, RequestId: {RequestId}",
            schemaId, userContext, HttpContext.TraceIdentifier);

        try
        {
            if (schemaId == Guid.Empty)
            {
                _logger.LogWarningWithCorrelation("Empty GUID provided for schemaId. User: {User}, RequestId: {RequestId}",
                    userContext, HttpContext.TraceIdentifier);
                return BadRequest("SchemaId cannot be empty");
            }

            var entities = await _repository.GetBySchemaIdAsync(schemaId);
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Delivery entities by schemaId. SchemaId: {SchemaId}, User: {User}, RequestId: {RequestId}",
                count, schemaId, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Delivery entities by schemaId. SchemaId: {SchemaId}, User: {User}, RequestId: {RequestId}",
                schemaId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Delivery entities");
        }
    }

    [HttpGet("version/{version}")]
    public async Task<ActionResult<IEnumerable<DeliveryEntity>>> GetByVersion(string version)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByVersion Delivery request. Version: {Version}, User: {User}, RequestId: {RequestId}",
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

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Delivery entities by version. Version: {Version}, User: {User}, RequestId: {RequestId}",
                count, version, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Delivery entities by version. Version: {Version}, User: {User}, RequestId: {RequestId}",
                version, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Delivery entities");
        }
    }

    [HttpGet("name/{name}")]
    public async Task<ActionResult<IEnumerable<DeliveryEntity>>> GetByName(string name)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByName Delivery request. Name: {Name}, User: {User}, RequestId: {RequestId}",
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

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Delivery entities by name. Name: {Name}, User: {User}, RequestId: {RequestId}",
                count, name, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Delivery entities by name. Name: {Name}, User: {User}, RequestId: {RequestId}",
                name, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Delivery entities");
        }
    }

    [HttpPost]
    public async Task<ActionResult<DeliveryEntity>> Create([FromBody] DeliveryEntity entity)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        var compositeKey = entity?.GetCompositeKey() ?? "Unknown";

        _logger.LogInformationWithCorrelation("Starting Create Delivery request. CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            compositeKey, userContext, HttpContext.TraceIdentifier);

        if (entity == null)
        {
            _logger.LogWarningWithCorrelation("Null entity provided for Delivery creation. User: {User}, RequestId: {RequestId}",
                userContext, HttpContext.TraceIdentifier);
            return BadRequest("Delivery entity cannot be null");
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarningWithCorrelation("Invalid model state for Delivery creation. Errors: {Errors}, User: {User}, RequestId: {RequestId}",
                errorMessage, userContext, HttpContext.TraceIdentifier);
            return BadRequest(ModelState);
        }

        try
        {
            entity!.CreatedBy = userContext;
            entity.Id = Guid.Empty;

            // Validate schema exists before creating the entity
            _logger.LogDebugWithCorrelation("Validating schema reference before creating Delivery entity. SchemaId: {SchemaId}, User: {User}, RequestId: {RequestId}",
                entity.SchemaId, userContext, HttpContext.TraceIdentifier);

            await _schemaValidator.ValidateSchemaExists(entity.SchemaId);

            _logger.LogDebugWithCorrelation("Schema validation passed. Creating Delivery entity with details. Version: {Version}, Name: {Name}, Payload: {Payload}, SchemaId: {SchemaId}, CreatedBy: {CreatedBy}, User: {User}, RequestId: {RequestId}",
                entity.Version, entity.Name, entity.Payload, entity.SchemaId, entity.CreatedBy, userContext, HttpContext.TraceIdentifier);

            var created = await _repository.CreateAsync(entity);

            if (created.Id == Guid.Empty)
            {
                _logger.LogErrorWithCorrelation("MongoDB failed to generate ID for new DeliveryEntity. Version: {Version}, Name: {Name}, User: {User}, RequestId: {RequestId}",
                    entity.Version, entity.Name, userContext, HttpContext.TraceIdentifier);
                return StatusCode(500, "Failed to generate entity ID");
            }

            _logger.LogInformationWithCorrelation("Successfully created Delivery entity. Id: {Id}, Version: {Version}, Name: {Name}, Payload: {Payload}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                created.Id, created.Version, created.Name, created.Payload, created.GetCompositeKey(), userContext, HttpContext.TraceIdentifier);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Schema") || ex.Message.Contains("schema"))
        {
            _logger.LogWarningWithCorrelation(ex, "Schema validation failed creating Delivery entity. SchemaId: {SchemaId}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.SchemaId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return BadRequest(new {
                error = "Schema validation failed",
                message = ex.Message,
                schemaId = entity?.SchemaId
            });
        }
        catch (DuplicateKeyException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Duplicate key conflict creating Delivery entity. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error creating Delivery entity. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while creating the Delivery entity");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<DeliveryEntity>> Update(string id, [FromBody] DeliveryEntity entity)
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

        _logger.LogInformationWithCorrelation("Starting Update Delivery request. Id: {Id}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            guidId, compositeKey, userContext, HttpContext.TraceIdentifier);

        if (entity == null)
        {
            _logger.LogWarningWithCorrelation("Null entity provided for Delivery update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return BadRequest("Delivery entity cannot be null");
        }

        if (guidId != entity.Id)
        {
            _logger.LogWarningWithCorrelation("ID mismatch in Delivery update request. URL ID: {UrlId}, Entity ID: {EntityId}, User: {User}, RequestId: {RequestId}",
                guidId, entity.Id, userContext, HttpContext.TraceIdentifier);
            return BadRequest("ID in URL does not match entity ID");
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarningWithCorrelation("Invalid model state for Delivery update. Id: {Id}, Errors: {Errors}, User: {User}, RequestId: {RequestId}",
                guidId, errorMessage, userContext, HttpContext.TraceIdentifier);
            return BadRequest(ModelState);
        }

        try
        {
            var existingEntity = await _repository.GetByIdAsync(guidId);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Delivery entity not found for update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Delivery entity with ID {guidId} not found");
            }

            // Validate entity references before updating (check for references in Assignment entities)
            _logger.LogDebugWithCorrelation("Validating entity references before updating Delivery entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);

            try
            {
                await _entityReferenceValidator.ValidateEntityCanBeDeleted(guidId);
                _logger.LogDebugWithCorrelation("Entity reference validation passed for Delivery entity update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarningWithCorrelation("Delivery entity update blocked due to references or validation failure. Id: {Id}, Message: {Message}, User: {User}, RequestId: {RequestId}",
                    guidId, ex.Message, userContext, HttpContext.TraceIdentifier);
                return Conflict($"Cannot update Delivery entity: {ex.Message}");
            }

            // Validate schema exists before updating the entity
            _logger.LogDebugWithCorrelation("Validating schema reference before updating Delivery entity. SchemaId: {SchemaId}, User: {User}, RequestId: {RequestId}",
                entity.SchemaId, userContext, HttpContext.TraceIdentifier);

            await _schemaValidator.ValidateSchemaExists(entity.SchemaId);

            entity.UpdatedBy = userContext;
            entity.CreatedAt = existingEntity.CreatedAt;
            entity.CreatedBy = existingEntity.CreatedBy;

            _logger.LogDebugWithCorrelation("Schema validation passed. Updating Delivery entity with details. Id: {Id}, Version: {Version}, Name: {Name}, Payload: {Payload}, SchemaId: {SchemaId}, UpdatedBy: {UpdatedBy}, User: {User}, RequestId: {RequestId}",
                entity.Id, entity.Version, entity.Name, entity.Payload, entity.SchemaId, entity.UpdatedBy, userContext, HttpContext.TraceIdentifier);

            var updated = await _repository.UpdateAsync(entity);

            _logger.LogInformationWithCorrelation("Successfully updated Delivery entity. Id: {Id}, Version: {Version}, Name: {Name}, Payload: {Payload}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                updated.Id, updated.Version, updated.Name, updated.Payload, updated.GetCompositeKey(), userContext, HttpContext.TraceIdentifier);

            return Ok(updated);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Schema") || ex.Message.Contains("schema"))
        {
            _logger.LogWarningWithCorrelation(ex, "Schema validation failed updating Delivery entity. Id: {Id}, SchemaId: {SchemaId}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.SchemaId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return BadRequest(new {
                error = "Schema validation failed",
                message = ex.Message,
                schemaId = entity?.SchemaId
            });
        }
        catch (DuplicateKeyException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Duplicate key conflict updating Delivery entity. Id: {Id}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error updating Delivery entity. Id: {Id}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while updating the Delivery entity");
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

        _logger.LogInformationWithCorrelation("Starting Delete Delivery request. Id: {Id}, User: {User}, RequestId: {RequestId}",
            guidId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(guidId);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Delivery entity not found for deletion. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Delivery entity with ID {guidId} not found");
            }

            // Validate entity can be deleted (check for references in Assignment entities)
            try
            {
                await _entityReferenceValidator.ValidateEntityCanBeDeleted(guidId);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarningWithCorrelation("Delivery entity deletion blocked due to references or validation failure. Id: {Id}, Message: {Message}, User: {User}, RequestId: {RequestId}",
                    guidId, ex.Message, userContext, HttpContext.TraceIdentifier);
                return Conflict($"Cannot delete Delivery entity: {ex.Message}");
            }

            _logger.LogDebugWithCorrelation("Deleting Delivery entity. Id: {Id}, Version: {Version}, Name: {Name}, Payload: {Payload}, User: {User}, RequestId: {RequestId}",
                guidId, existingEntity.Version, existingEntity.Name, existingEntity.Payload, userContext, HttpContext.TraceIdentifier);

            var success = await _repository.DeleteAsync(guidId);

            if (!success)
            {
                _logger.LogErrorWithCorrelation("Failed to delete Delivery entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return StatusCode(500, "Failed to delete Delivery entity");
            }

            _logger.LogInformationWithCorrelation("Successfully deleted Delivery entity. Id: {Id}, Version: {Version}, Name: {Name}, Payload: {Payload}, User: {User}, RequestId: {RequestId}",
                guidId, existingEntity.Version, existingEntity.Name, existingEntity.Payload, userContext, HttpContext.TraceIdentifier);

            return NoContent();
        }
        
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error deleting Delivery entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while deleting the Delivery entity");
        }
    }

    [HttpGet("composite")]
    public ActionResult<DeliveryEntity> GetByCompositeKeyEmpty()
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
    /// Check if a schema is referenced by any delivery entities
    /// </summary>
    /// <param name="schemaId">The schema ID to check</param>
    /// <returns>True if schema is referenced, false otherwise</returns>
    [HttpGet("schema/{schemaId}/exists")]
    public async Task<ActionResult<bool>> CheckSchemaReference(Guid schemaId)
    {
        var requestId = HttpContext.TraceIdentifier;
        var user = HttpContext.User?.Identity?.Name ?? "Anonymous";

        _logger.LogInformationWithCorrelation("Starting schema reference check. SchemaId: {SchemaId}, User: {User}, RequestId: {RequestId}",
            schemaId, user, requestId);

        try
        {
            if (schemaId == Guid.Empty)
            {
                _logger.LogWarningWithCorrelation("Invalid schema ID provided for reference check. SchemaId: {SchemaId}, User: {User}, RequestId: {RequestId}",
                    schemaId, user, requestId);
                return BadRequest("Schema ID cannot be empty");
            }

            var hasReferences = await _repository.HasSchemaReferences(schemaId);

            _logger.LogInformationWithCorrelation("Schema reference check completed. SchemaId: {SchemaId}, HasReferences: {HasReferences}, User: {User}, RequestId: {RequestId}",
                schemaId, hasReferences, user, requestId);

            return Ok(hasReferences);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error checking schema references. SchemaId: {SchemaId}, User: {User}, RequestId: {RequestId}",
                schemaId, user, requestId);
            return StatusCode(500, "Internal server error while checking schema references");
        }
    }
}
