using Manager.Schema.Repositories;
using Manager.Schema.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Entities;
using Shared.Correlation;    
using Shared.Exceptions;
using Swashbuckle.AspNetCore.Annotations;

namespace Manager.Schema.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Schema management operations")]
public class SchemaController : ControllerBase
{
    private readonly ISchemaEntityRepository _repository;
    private readonly ISchemaReferenceValidator _referenceValidator;
    private readonly ISchemaBreakingChangeAnalyzer _breakingChangeAnalyzer;
    private readonly ILogger<SchemaController> _logger;

    public SchemaController(
        ISchemaEntityRepository repository,
        ISchemaReferenceValidator referenceValidator,
        ISchemaBreakingChangeAnalyzer breakingChangeAnalyzer,
        ILogger<SchemaController> logger)
    {
        _repository = repository;
        _referenceValidator = referenceValidator;
        _breakingChangeAnalyzer = breakingChangeAnalyzer;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SchemaEntity>>> GetAll()
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetAll schemas request. User: {User}, RequestId: {RequestId}",
            userContext, HttpContext.TraceIdentifier);

        try
        {
            var entities = await _repository.GetAllAsync();
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Schema entities. User: {User}, RequestId: {RequestId}",
                count, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving all Schema entities. User: {User}, RequestId: {RequestId}",
                userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Schema entities");
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

            _logger.LogInformationWithCorrelation("Successfully retrieved paged Schema entities. Page: {Page}/{TotalPages}, PageSize: {PageSize}, TotalCount: {TotalCount}, User: {User}, RequestId: {RequestId}",
                page, totalPages, pageSize, totalCount, userContext, HttpContext.TraceIdentifier);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving paged Schema entities. Page: {Page}, PageSize: {PageSize}, User: {User}, RequestId: {RequestId}",
                originalPage, originalPageSize, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving paged Schema entities");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SchemaEntity>> GetById(string id)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(id, out Guid guidId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided. Id: {Id}, User: {User}, RequestId: {RequestId}",
                id, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {id}");
        }

        _logger.LogInformationWithCorrelation("Starting GetById Schema request. Id: {Id}, User: {User}, RequestId: {RequestId}",
            guidId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entity = await _repository.GetByIdAsync(guidId);

            if (entity == null)
            {
                _logger.LogWarningWithCorrelation("Schema entity not found. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Schema entity with ID {guidId} not found");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved Schema entity. Id: {Id}, Version: {Version}, Name: {Name}, Definition: {Definition}, User: {User}, RequestId: {RequestId}",
                entity.Id, entity.Version, entity.Name, entity.Definition, userContext, HttpContext.TraceIdentifier);

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Schema entity by ID. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving the Schema entity");
        }
    }

    [HttpGet("composite/{version}/{name}")]
    public async Task<ActionResult<SchemaEntity>> GetByCompositeKey(string version, string name)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        // SchemaEntity composite key format: "version_name"
        var compositeKey = $"{version}_{name}";

        _logger.LogInformationWithCorrelation("Starting GetByCompositeKey Schema request. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
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
                _logger.LogWarningWithCorrelation("Schema entity not found by composite key. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                    version, name, compositeKey, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Schema entity with version '{version}' and name '{name}' not found");
            }

            _logger.LogInformationWithCorrelation("Successfully retrieved Schema entity by composite key. CompositeKey: {CompositeKey}, Id: {Id}, Version: {Version}, Name: {Name}, Definition: {Definition}, User: {User}, RequestId: {RequestId}",
                compositeKey, entity.Id, entity.Version, entity.Name, entity.Definition, userContext, HttpContext.TraceIdentifier);

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Schema entity by composite key. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                version, name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving the Schema entity");
        }
    }

    [HttpGet("definition/{definition}")]
    public async Task<ActionResult<IEnumerable<SchemaEntity>>> GetByDefinition(string definition)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByDefinition Schema request. Definition: {Definition}, User: {User}, RequestId: {RequestId}",
            definition, userContext, HttpContext.TraceIdentifier);

        try
        {
            if (string.IsNullOrWhiteSpace(definition))
            {
                _logger.LogWarningWithCorrelation("Empty or null definition provided. User: {User}, RequestId: {RequestId}",
                    userContext, HttpContext.TraceIdentifier);
                return BadRequest("Definition cannot be empty");
            }

            var entities = await _repository.GetByDefinitionAsync(definition);
            var count = entities.Count();

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Schema entities by definition. Definition: {Definition}, User: {User}, RequestId: {RequestId}",
                count, definition, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Schema entities by definition. Definition: {Definition}, User: {User}, RequestId: {RequestId}",
                definition, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Schema entities");
        }
    }

    [HttpGet("version/{version}")]
    public async Task<ActionResult<IEnumerable<SchemaEntity>>> GetByVersion(string version)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByVersion Schema request. Version: {Version}, User: {User}, RequestId: {RequestId}",
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

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Schema entities by version. Version: {Version}, User: {User}, RequestId: {RequestId}",
                count, version, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Schema entities by version. Version: {Version}, User: {User}, RequestId: {RequestId}",
                version, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Schema entities");
        }
    }

    [HttpGet("name/{name}")]
    public async Task<ActionResult<IEnumerable<SchemaEntity>>> GetByName(string name)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        _logger.LogInformationWithCorrelation("Starting GetByName Schema request. Name: {Name}, User: {User}, RequestId: {RequestId}",
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

            _logger.LogInformationWithCorrelation("Successfully retrieved {Count} Schema entities by name. Name: {Name}, User: {User}, RequestId: {RequestId}",
                count, name, userContext, HttpContext.TraceIdentifier);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error retrieving Schema entities by name. Name: {Name}, User: {User}, RequestId: {RequestId}",
                name, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while retrieving Schema entities");
        }
    }

    [HttpGet("{id}/exists")]
    public async Task<ActionResult<bool>> Exists(string id)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";

        // Validate GUID format
        if (!Guid.TryParse(id, out Guid guidId))
        {
            _logger.LogWarningWithCorrelation("Invalid GUID format provided for exists check. Id: {Id}, User: {User}, RequestId: {RequestId}",
                id, userContext, HttpContext.TraceIdentifier);
            return BadRequest($"Invalid GUID format: {id}");
        }

        _logger.LogDebugWithCorrelation("Starting Schema exists check. Id: {Id}, User: {User}, RequestId: {RequestId}",
            guidId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var entity = await _repository.GetByIdAsync(guidId);
            var exists = entity != null;

            _logger.LogDebugWithCorrelation("Schema exists check completed. Id: {Id}, Exists: {Exists}, User: {User}, RequestId: {RequestId}",
                guidId, exists, userContext, HttpContext.TraceIdentifier);

            return Ok(exists);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error checking Schema existence. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while checking Schema existence");
        }
    }

    [HttpPost]
    public async Task<ActionResult<SchemaEntity>> Create([FromBody] SchemaEntity entity)
    {
        var userContext = User.Identity?.Name ?? "Anonymous";
        var compositeKey = entity?.GetCompositeKey() ?? "Unknown";

        _logger.LogInformationWithCorrelation("Starting Create Schema request. CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            compositeKey, userContext, HttpContext.TraceIdentifier);

        if (entity == null)
        {
            _logger.LogWarningWithCorrelation("Null entity provided for Schema creation. User: {User}, RequestId: {RequestId}",
                userContext, HttpContext.TraceIdentifier);
            return BadRequest("Schema entity cannot be null");
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarningWithCorrelation("Invalid model state for Schema creation. Errors: {Errors}, User: {User}, RequestId: {RequestId}",
                errorMessage, userContext, HttpContext.TraceIdentifier);
            return BadRequest(ModelState);
        }

        try
        {
            entity!.CreatedBy = userContext;
            entity.Id = Guid.Empty;

            _logger.LogDebugWithCorrelation("Creating Schema entity with details. Version: {Version}, Name: {Name}, Definition: {Definition}, CreatedBy: {CreatedBy}, User: {User}, RequestId: {RequestId}",
                entity.Version, entity.Name, entity.Definition, entity.CreatedBy, userContext, HttpContext.TraceIdentifier);

            var created = await _repository.CreateAsync(entity);

            if (created.Id == Guid.Empty)
            {
                _logger.LogErrorWithCorrelation("MongoDB failed to generate ID for new SchemaEntity. Version: {Version}, Name: {Name}, User: {User}, RequestId: {RequestId}",
                    entity.Version, entity.Name, userContext, HttpContext.TraceIdentifier);
                return StatusCode(500, "Failed to generate entity ID");
            }

            _logger.LogInformationWithCorrelation("Successfully created Schema entity. Id: {Id}, Version: {Version}, Name: {Name}, Definition: {Definition}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                created.Id, created.Version, created.Name, created.Definition, created.GetCompositeKey(), userContext, HttpContext.TraceIdentifier);

            // Return 201 Created with location header
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (DuplicateKeyException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Duplicate key conflict creating Schema entity. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error creating Schema entity. Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while creating the Schema entity");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SchemaEntity>> Update(string id, [FromBody] SchemaEntity entity)
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

        _logger.LogInformationWithCorrelation("Starting Update Schema request. Id: {Id}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
            guidId, compositeKey, userContext, HttpContext.TraceIdentifier);

        if (entity == null)
        {
            _logger.LogWarningWithCorrelation("Null entity provided for Schema update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return BadRequest("Schema entity cannot be null");
        }

        if (guidId != entity.Id)
        {
            _logger.LogWarningWithCorrelation("ID mismatch in Schema update request. URL ID: {UrlId}, Entity ID: {EntityId}, User: {User}, RequestId: {RequestId}",
                guidId, entity.Id, userContext, HttpContext.TraceIdentifier);
            return BadRequest("ID in URL does not match entity ID");
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarningWithCorrelation("Invalid model state for Schema update. Id: {Id}, Errors: {Errors}, User: {User}, RequestId: {RequestId}",
                guidId, errorMessage, userContext, HttpContext.TraceIdentifier);
            return BadRequest(ModelState);
        }

        try
        {
            var existingEntity = await _repository.GetByIdAsync(guidId);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Schema entity not found for update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Schema entity with ID {guidId} not found");
            }

            // Check if this is a breaking schema change that could affect dependent entities
            var isBreakingChange = IsBreakingSchemaChange(existingEntity, entity);
            if (isBreakingChange)
            {
                _logger.LogInformationWithCorrelation("Validating schema references before breaking update. Id: {Id}, Version: {Version}, Name: {Name}, User: {User}, RequestId: {RequestId}",
                    guidId, existingEntity.Version, existingEntity.Name, userContext, HttpContext.TraceIdentifier);

                // Check for references in other entities before allowing breaking updates
                var hasReferences = await _referenceValidator.HasReferences(guidId);
                if (hasReferences)
                {
                    _logger.LogWarningWithCorrelation("Cannot update Schema entity with breaking changes - it has references in other entities. Id: {Id}, Version: {Version}, Name: {Name}, User: {User}, RequestId: {RequestId}",
                        guidId, existingEntity.Version, existingEntity.Name, userContext, HttpContext.TraceIdentifier);

                    return Conflict(new
                    {
                        error = "Schema has references",
                        message = "Cannot update schema with breaking changes because it is referenced by other entities (Address, Delivery, or Processor entities). Consider creating a new schema version instead.",
                        schemaId = guidId,
                        version = existingEntity.Version,
                        name = existingEntity.Name,
                        breakingChanges = GetBreakingChangeDetails(existingEntity, entity)
                    });
                }

                _logger.LogDebugWithCorrelation("No references found - proceeding with breaking Schema entity update. Id: {Id}, Version: {Version}, Name: {Name}, User: {User}, RequestId: {RequestId}",
                    guidId, existingEntity.Version, existingEntity.Name, userContext, HttpContext.TraceIdentifier);
            }

            entity.UpdatedBy = userContext;
            entity.CreatedAt = existingEntity.CreatedAt;
            entity.CreatedBy = existingEntity.CreatedBy;

            _logger.LogDebugWithCorrelation("Updating Schema entity with details. Id: {Id}, Version: {Version}, Name: {Name}, Definition: {Definition}, UpdatedBy: {UpdatedBy}, IsBreakingChange: {IsBreakingChange}, User: {User}, RequestId: {RequestId}",
                entity.Id, entity.Version, entity.Name, entity.Definition, entity.UpdatedBy, isBreakingChange, userContext, HttpContext.TraceIdentifier);

            var updated = await _repository.UpdateAsync(entity);

            _logger.LogInformationWithCorrelation("Successfully updated Schema entity. Id: {Id}, Version: {Version}, Name: {Name}, Definition: {Definition}, CompositeKey: {CompositeKey}, IsBreakingChange: {IsBreakingChange}, User: {User}, RequestId: {RequestId}",
                updated.Id, updated.Version, updated.Name, updated.Definition, updated.GetCompositeKey(), isBreakingChange, userContext, HttpContext.TraceIdentifier);

            return Ok(updated);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Schema reference validation"))
        {
            _logger.LogErrorWithCorrelation(ex, "Schema reference validation failed during update. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(503, new
            {
                error = "Validation service unavailable",
                message = "Cannot validate schema references - one or more validation services are unavailable. Operation rejected for safety.",
                details = ex.Message
            });
        }
        catch (DuplicateKeyException ex)
        {
            _logger.LogWarningWithCorrelation(ex, "Duplicate key conflict updating Schema entity. Id: {Id}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error updating Schema entity. Id: {Id}, Version: {Version}, Name: {Name}, CompositeKey: {CompositeKey}, User: {User}, RequestId: {RequestId}",
                guidId, entity?.Version, entity?.Name, compositeKey, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while updating the Schema entity");
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

        _logger.LogInformationWithCorrelation("Starting Delete Schema request. Id: {Id}, User: {User}, RequestId: {RequestId}",
            guidId, userContext, HttpContext.TraceIdentifier);

        try
        {
            var existingEntity = await _repository.GetByIdAsync(guidId);
            if (existingEntity == null)
            {
                _logger.LogWarningWithCorrelation("Schema entity not found for deletion. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return NotFound($"Schema entity with ID {guidId} not found");
            }

            _logger.LogInformationWithCorrelation("Validating schema references before deletion. Id: {Id}, Version: {Version}, Name: {Name}, User: {User}, RequestId: {RequestId}",
                guidId, existingEntity.Version, existingEntity.Name, userContext, HttpContext.TraceIdentifier);

            // Check for references in other entities before allowing deletion
            var hasReferences = await _referenceValidator.HasReferences(guidId);
            if (hasReferences)
            {
                _logger.LogWarningWithCorrelation("Cannot delete Schema entity - it has references in other entities. Id: {Id}, Version: {Version}, Name: {Name}, User: {User}, RequestId: {RequestId}",
                    guidId, existingEntity.Version, existingEntity.Name, userContext, HttpContext.TraceIdentifier);

                return Conflict(new
                {
                    error = "Schema has references",
                    message = "Cannot delete schema because it is referenced by other entities (Address, Delivery, or Processor entities)",
                    schemaId = guidId,
                    version = existingEntity.Version,
                    name = existingEntity.Name
                });
            }

            _logger.LogDebugWithCorrelation("No references found - proceeding with Schema entity deletion. Id: {Id}, Version: {Version}, Name: {Name}, Definition: {Definition}, User: {User}, RequestId: {RequestId}",
                guidId, existingEntity.Version, existingEntity.Name, existingEntity.Definition, userContext, HttpContext.TraceIdentifier);

            var success = await _repository.DeleteAsync(guidId);

            if (!success)
            {
                _logger.LogErrorWithCorrelation("Failed to delete Schema entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                    guidId, userContext, HttpContext.TraceIdentifier);
                return StatusCode(500, "Failed to delete Schema entity");
            }

            _logger.LogInformationWithCorrelation("Successfully deleted Schema entity. Id: {Id}, Version: {Version}, Name: {Name}, Definition: {Definition}, User: {User}, RequestId: {RequestId}",
                guidId, existingEntity.Version, existingEntity.Name, existingEntity.Definition, userContext, HttpContext.TraceIdentifier);

            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Schema reference validation"))
        {
            _logger.LogErrorWithCorrelation(ex, "Schema reference validation failed during deletion. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(503, new
            {
                error = "Validation service unavailable",
                message = "Cannot validate schema references - one or more validation services are unavailable. Operation rejected for safety.",
                details = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error deleting Schema entity. Id: {Id}, User: {User}, RequestId: {RequestId}",
                guidId, userContext, HttpContext.TraceIdentifier);
            return StatusCode(500, "An error occurred while deleting the Schema entity");
        }
    }

    [HttpGet("composite")]
    public ActionResult<SchemaEntity> GetByCompositeKeyEmpty()
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
    /// Determines if the schema update contains breaking changes that could affect dependent entities
    /// </summary>
    /// <param name="existingEntity">The current schema entity</param>
    /// <param name="updatedEntity">The updated schema entity</param>
    /// <returns>True if the update contains breaking changes</returns>
    private bool IsBreakingSchemaChange(SchemaEntity existingEntity, SchemaEntity updatedEntity)
    {
        return _breakingChangeAnalyzer.IsBreakingChange(existingEntity.Definition, updatedEntity.Definition);
    }

    /// <summary>
    /// Gets detailed information about breaking changes for error responses
    /// </summary>
    /// <param name="existingEntity">The current schema entity</param>
    /// <param name="updatedEntity">The updated schema entity</param>
    /// <returns>List of breaking change descriptions</returns>
    private List<string> GetBreakingChangeDetails(SchemaEntity existingEntity, SchemaEntity updatedEntity)
    {
        return _breakingChangeAnalyzer.GetBreakingChangeDetails(existingEntity.Definition, updatedEntity.Definition);
    }
}
