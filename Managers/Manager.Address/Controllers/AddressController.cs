using Manager.Address.Repositories;
using Manager.Address.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Entities;
using Shared.Correlation;
using Shared.Exceptions;
using Swashbuckle.AspNetCore.Annotations;

namespace Manager.Address.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Address management operations")]
public class AddressController : ControllerBase
{
    private readonly IAddressEntityRepository _repository;
    private readonly IEntityReferenceValidator _entityReferenceValidator;
    private readonly ISchemaValidationService _schemaValidator;
    private readonly ILogger<AddressController> _logger;

    public AddressController(
        IAddressEntityRepository repository,
        IEntityReferenceValidator entityReferenceValidator,
        ISchemaValidationService schemaValidator,
        ILogger<AddressController> logger)
    {
        _repository = repository;
        _entityReferenceValidator = entityReferenceValidator;
        _schemaValidator = schemaValidator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AddressEntity>>> GetAll()
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetAll schemas request. User: {User}, RequestId: {RequestId}",
            userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetAllAsync();
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Address entities. User: {User}, RequestId: {RequestId}",
                count, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving all Address entities. User: {User}, RequestId: {RequestId}",
                userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Address entities");
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

            _logger.LogInformationWithCorrelation("Successfully retrieved paged Address entities. Page: {Page}/{TotalPages}, PageSize: {PageSize}, TotalCount: {TotalCount}, User: {User}, RequestId: {RequestId}",
                page, totalPages, pageSize, totalCount, userContext, HttpContext.TraceIdentifier);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving paged Address entities. Page: {Page}, PageSize: {PageSize}, User: {User}, RequestId: {RequestId}",
                originalPage, originalPageSize, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving paged Address entities");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AddressEntity>> GetById(string id)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(id, out Guid guidId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided. Id: {Id}, User: {User}, RequestId: {RequestId}",
                id, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {id}");
        }

        _logger.LogInformationWithCorrelation("Starting GetById Address request. Id: {Id}, User: {User}, RequestId: {RequestId}",
            guidId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entity = await _repository.GetByIdAsync(guidId);

            if (entity == null)
            {
                _logger.LogWarningWithCorrelation("Address entity not found. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Address entity with ID {guidId} not found");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved Address entity. Id: {Id}, Version: {Version}, Name: {Name}, ConnectionString: {ConnectionString}, User: {User}, RequestId: {RequestId}",
                entity.Id, entity.Version, entity.Name, entity.ConnectionString, userContext, HttpContext.TraceIdentifier);

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Address entity by ID. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving the Address entity");
        }
    }

    [HttpGet("composite/{connectionString}")]
    public async Task<ActionResult<AddressEntity>> GetByCompositeKey(string connectionString)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        // AddressEntity composite key format: "connectionString"
        var compositeKey = connectionString;

        _logger.LogInformationWithCorrelation("Starting GetByCompositeKey Address request. ConnectionString: {ConnectionString}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            connectionString, compositeKey, userContext, HttpContext.TraceIdentifier);

        try
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogWarningWithCorrelation("Empty or null connectionString provided. ConnectionString: {ConnectionString}, User: {User}, RequestId: {RequestId}",
                    connectionString, userContext, HttpContext.TraceIdentifier);
                return BadRequest("ConnectionString cannot be empty");
            }

            var entity = await _repository.GetByCompositeKeyAsync(compositeKey);

            if (entity == null)
            {
                _logger.LogWarningWithCorrelation("Address entity not found by composite key. CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                    compositeKey, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Address entity with connectionString '{connectionString}' not found");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved Address entity by composite key. CompositeKey: {CompositeKey}, Id: {Id}, Version: {Version}, Name: {Name}, ConnectionString: {ConnectionString}, User: {User}, RequestId: {RequestId}",
                compositeKey, entity.Id, entity.Version, entity.Name, entity.ConnectionString, userContext, HttpContext.TraceIdentifier);

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Address entity by composite key. CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving the Address entity");
        }
    }



    [HttpGet("version/{version}")]
    public async Task<ActionResult<IEnumerable<AddressEntity>>> GetByVersion(string version)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByVersion Address request. Version: {Version}, User: {User}, RequestId: {RequestId}",
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

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Address entities by version. Version: {Version}, User: {User}, RequestId: {RequestId}",
                count, version, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Address entities by version. Version: {Version}, User: {User}, RequestId: {RequestId}",
                version, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Address entities");
        }
    }

    [HttpGet("name/{name}")]
    public async Task<ActionResult<IEnumerable<AddressEntity>>> GetByName(string name)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByName Address request. Name: {Name}, User: {User}, RequestId: {RequestId}",
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

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Address entities by name. Name: {Name}, User: {User}, RequestId: {RequestId}",
                count, name, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Address entities by name. Name: {Name}, User: {User}, RequestId: {RequestId}",
                name, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Address entities");
        }
    }

    [HttpPost]
    public async Task<ActionResult<AddressEntity>> Create([FromBody] AddressEntity entity)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        var compositeKey = entity?.GetCompositeKey() ?? "Unknown";

        _logger.LogInformationWithCorrelation("Starting Create Address request. CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            compositeKey, userContext, HttpContext.TraceIdentifier);

        if (entity == null)
        {
            _logger.LogWarningWithCorrelation("Null entity provided for Address creation. User: {User}, RequestId: {RequestId}",
                userContext, HttpContext.TraceIdentifier);
            return BadRequest("Address entity cannot be null");
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarningWithCorrelation("Invalid model state for Address creation. Errors: {Errors}, User: {User}, RequestId: {RequestId}",
                errorMessage, userContext, HttpContext.TraceIdentifier);
            return BadRequest(ModelState);
        }

        try
        {
            entity!.CreatedBy = userContext;
            entity.Id = Guid.Empty;

            // Validate schema exists before creating the entity
            _logger.LogDebugWithCorrelation("Validating schema reference before creating Address entity. SchemaId: {SchemaId}, User: {User}, RequestId: {RequestId}",
                entity.SchemaId, userContext, HttpContext.TraceIdentifier);

            await _schemaValidator.ValidateSchemaExists(entity.SchemaId);

            _logger.LogDebugWithCorrelation("Schema validation passed. Creating Address entity with details. Version: {Version}, Name: {Name}, ConnectionString: {ConnectionString}, SchemaId: {SchemaId}, CreatedBy: {CreatedBy}, User: {User}, RequestId: {RequestId}",
                entity.Version, entity.Name, entity.ConnectionString, entity.SchemaId, entity.CreatedBy, userContext, HttpContext.TraceIdentifier);

            var created = await _repository.CreateAsync(entity);

            if (created.Id == Guid.Empty)
            {
                _logger.LogErrorWithCorrelation("MongoDB failed to generate ID for new AddressEntity. Version: {Version}, Name: {Name}, User: {User}, RequestId: {RequestId}",
                    entity.Version, entity.Name, userContext, HttpContext.TraceIdentifier);
                return StatusCode(500, "Failed to generate entity ID");
            }

            _logger.LogInformationWithCorrelation("Successfully created Address entity. Id: {Id}, Version: {Version}, Name: {Name}, ConnectionString: {ConnectionString}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                created.Id, created.Version, created.Name, created.ConnectionString, created.GetCompositeKey(), userContext, HttpContext.TraceIdentifier);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Schema") || ex.Message.Contains("schema"))
        {
            _logger.LogWarningWithCorrelation(ex, "Schema validation failed creating Address entity. SchemaId: {SchemaId}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.SchemaId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return BadRequest(new {
                error = "Schema validation failed",
                message = ex.Message,
                schemaId = entity?.SchemaId
            });
        }
        catch (DuplicateKeyException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Duplicate key conflict creating Address entity. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error creating Address entity. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while creating the Address entity");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<AddressEntity>> Update(string id, [FromBody] AddressEntity entity)
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

        _logger.LogInformationWithCorrelation("Starting Update Address request. Id: {Id}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            guidId, compositeKey, userContext, HttpContext.TraceIdentifier);

        if (entity == null)
        {
            _logger.LogWarningWithCorrelation("Null entity provided for Address update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return BadRequest("Address entity cannot be null");
        }

        if (guidId != entity.Id)
        {
            _logger.LogWarningWithCorrelation("ID mismatch in Address update request. URL ID: {UrlId}, Entity ID: {EntityId}, User: {User}, RequestId: {RequestId}",
                guidId, entity.Id, userContext, HttpContext.TraceIdentifier);
            return BadRequest("ID in URL does not match entity ID");
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarningWithCorrelation("Invalid model state for Address update. Id: {Id}, Errors: {Errors}, User: {User}, RequestId: {RequestId}",
                guidId, errorMessage, userContext, HttpContext.TraceIdentifier);
            return BadRequest(ModelState);
        }

        try
        {
            var existingEntity = await _repository.GetByIdAsync(guidId);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Address entity not found for update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Address entity with ID {guidId} not found");
            }

            // Validate entity can be updated (check for references in Assignment entities)
            _logger.LogDebugWithCorrelation("Validating entity references before updating Address entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);

            try
            {
                await _entityReferenceValidator.ValidateEntityCanBeDeleted(guidId);

                _logger.LogDebugWithCorrelation("Entity reference validation passed for Address entity update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarningWithCorrelation("Address entity update blocked due to references or validation failure. Id: {Id}, Message: {Message}, User: {User}, RequestId: {RequestId}",
                    guidId, ex.Message, userContext, HttpContext.TraceIdentifier);
                return Conflict(new
                {
                    error = "Address has references",
                    message = $"Cannot update Address entity: {ex.Message}",
                    addressId = guidId,
                    referencedBy = "AssignmentEntity.EntityIds"
                });
            }

            // Validate schema exists before updating the entity
            _logger.LogDebugWithCorrelation("Validating schema reference before updating Address entity. SchemaId: {SchemaId}, User: {User}, RequestId: {RequestId}",
                entity.SchemaId, userContext, HttpContext.TraceIdentifier);

            await _schemaValidator.ValidateSchemaExists(entity.SchemaId);

            entity.UpdatedBy = userContext;
            entity.CreatedAt = existingEntity.CreatedAt;
            entity.CreatedBy = existingEntity.CreatedBy;

            _logger.LogDebugWithCorrelation("Schema validation passed. Updating Address entity with details. Id: {Id}, Version: {Version}, Name: {Name}, ConnectionString: {ConnectionString}, SchemaId: {SchemaId}, UpdatedBy: {UpdatedBy}, User: {User}, RequestId: {RequestId}",
                entity.Id, entity.Version, entity.Name, entity.ConnectionString, entity.SchemaId, entity.UpdatedBy, userContext, HttpContext.TraceIdentifier);

            var updated = await _repository.UpdateAsync(entity);

            _logger.LogInformationWithCorrelation("Successfully updated Address entity. Id: {Id}, Version: {Version}, Name: {Name}, ConnectionString: {ConnectionString}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                updated.Id, updated.Version, updated.Name, updated.ConnectionString, updated.GetCompositeKey(), userContext, HttpContext.TraceIdentifier);

            return Ok(updated);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Schema") || ex.Message.Contains("schema"))
        {
            _logger.LogWarningWithCorrelation(ex, "Schema validation failed updating Address entity. Id: {Id}, SchemaId: {SchemaId}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.SchemaId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return BadRequest(new {
                error = "Schema validation failed",
                message = ex.Message,
                schemaId = entity?.SchemaId
            });
        }
        catch (DuplicateKeyException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Duplicate key conflict updating Address entity. Id: {Id}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error updating Address entity. Id: {Id}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while updating the Address entity");
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

        _logger.LogInformationWithCorrelation("Starting Delete Address request. Id: {Id}, User: {User}, RequestId: {RequestId}",
            guidId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(guidId);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Address entity not found for deletion. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Address entity with ID {guidId} not found");
            }

            // Validate entity can be deleted (check for references in Assignment entities)
            try
            {
                await _entityReferenceValidator.ValidateEntityCanBeDeleted(guidId);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarningWithCorrelation("Address entity deletion blocked due to references or validation failure. Id: {Id}, Message: {Message}, User: {User}, RequestId: {RequestId}",
                    guidId, ex.Message, userContext, HttpContext.TraceIdentifier);
                return Conflict($"Cannot delete Address entity: {ex.Message}");
            }

            _logger.LogDebugWithCorrelation("Deleting Address entity. Id: {Id}, Version: {Version}, Name: {Name}, ConnectionString: {ConnectionString}, User: {User}, RequestId: {RequestId}",
                guidId, existingEntity.Version, existingEntity.Name, existingEntity.ConnectionString, userContext, HttpContext.TraceIdentifier);

            var success = await _repository.DeleteAsync(guidId);

            if (!success)
            {
                _logger.LogErrorWithCorrelation("Failed to delete Address entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return StatusCode(500, "Failed to delete Address entity");
            }

            _logger.LogInformationWithCorrelation("Successfully deleted Address entity. Id: {Id}, Version: {Version}, Name: {Name}, ConnectionString: {ConnectionString}, User: {User}, RequestId: {RequestId}",
                guidId, existingEntity.Version, existingEntity.Name, existingEntity.ConnectionString, userContext, HttpContext.TraceIdentifier);

            return NoContent();
        }
        
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error deleting Address entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while deleting the Address entity");
        }
    }

    [HttpGet("composite")]
    public ActionResult<AddressEntity> GetByCompositeKeyEmpty()
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        _logger.LogWarningWithCorrelation("Empty composite key parameters in GetByCompositeKey request (no parameters provided). User: {User}, RequestId: {RequestId}",
            userContext, HttpContext.TraceIdentifier);

        return BadRequest(new {
            error = "Invalid composite key parameters",
            message = "ConnectionString parameter is required",
            parameters = new[] { "connectionString" }
        });
    }

    /// <summary>
    /// Check if a schema is referenced by any address entities
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
