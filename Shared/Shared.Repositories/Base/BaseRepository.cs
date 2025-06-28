using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Shared.Correlation;
using Shared.Entities.Base;
using Shared.Exceptions;
using Shared.Repositories.Interfaces;
using Shared.Services;

namespace Shared.Repositories.Base;

/// <summary>
/// Abstract base repository class that provides common CRUD operations for all entities.
/// Implements the repository pattern with MongoDB as the data store.
/// </summary>
/// <typeparam name="T">The entity type that inherits from BaseEntity.</typeparam>
public abstract class BaseRepository<T> : IBaseRepository<T> where T : BaseEntity
{
    /// <summary>
    /// The MongoDB collection for the entity type.
    /// </summary>
    protected readonly IMongoCollection<T> _collection;

    /// <summary>
    /// Logger instance for the repository.
    /// </summary>
    protected readonly ILogger<BaseRepository<T>> _logger;

    /// <summary>
    /// Event publisher for domain events.
    /// </summary>
    protected readonly IEventPublisher _eventPublisher;

    /// <summary>
    /// Manager metrics service for recording business metrics.
    /// </summary>
    protected readonly IManagerMetricsService _metricsService;

    /// <summary>
    /// Activity source for distributed tracing.
    /// </summary>
    private static readonly ActivitySource ActivitySource = new($"Manager.Schemas.Repository.{typeof(T).Name}");

    /// <summary>
    /// Initializes a new instance of the BaseRepository class.
    /// </summary>
    /// <param name="database">The MongoDB database instance.</param>
    /// <param name="collectionName">The name of the collection for this entity type.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="eventPublisher">The event publisher for domain events.</param>
    /// <param name="metricsService">The manager metrics service for recording business metrics.</param>
    protected BaseRepository(IMongoDatabase database, string collectionName, ILogger<BaseRepository<T>> logger, IEventPublisher eventPublisher, IManagerMetricsService metricsService)
    {
        _collection = database.GetCollection<T>(collectionName);
        _logger = logger;
        _eventPublisher = eventPublisher;
        _metricsService = metricsService;
        CreateIndexes();
    }

    /// <summary>
    /// Gets an entity by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity.</param>
    /// <returns>The entity if found, otherwise null.</returns>
    public virtual async Task<T?> GetByIdAsync(Guid id)
    {
        using var activity = ActivitySource.StartActivity($"GetById{typeof(T).Name}");
        activity?.SetTag("entity.id", id.ToString());
        activity?.SetTag("entity.type", typeof(T).Name);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var filter = Builders<T>.Filter.Eq(x => x.Id, id);
            var result = await _collection.Find(filter).FirstOrDefaultAsync();

            activity?.SetTag("result.found", result != null);

            // Record operation metrics
            stopwatch.Stop();
            _metricsService.RecordOperation("get", typeof(T).Name.ToLowerInvariant(), stopwatch.Elapsed, true);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metricsService.RecordOperation("get", typeof(T).Name.ToLowerInvariant(), stopwatch.Elapsed, false);
            _logger.LogErrorWithCorrelation(ex, "Error getting {EntityType} by ID {Id}", typeof(T).Name, id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets an entity by its composite key.
    /// </summary>
    /// <param name="compositeKey">The composite key of the entity.</param>
    /// <returns>The entity if found, otherwise null.</returns>
    public virtual async Task<T?> GetByCompositeKeyAsync(string compositeKey)
    {
        using var activity = ActivitySource.StartActivity($"GetByCompositeKey{typeof(T).Name}");
        activity?.SetTag("entity.compositeKey", compositeKey);
        activity?.SetTag("entity.type", typeof(T).Name);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var filter = CreateCompositeKeyFilter(compositeKey);
            var result = await _collection.Find(filter).FirstOrDefaultAsync();

            stopwatch.Stop();
            _metricsService.RecordOperation("getbycompositekey", typeof(T).Name.ToLowerInvariant(), stopwatch.Elapsed, true);

            activity?.SetTag("result.found", result != null);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metricsService.RecordOperation("getbycompositekey", typeof(T).Name.ToLowerInvariant(), stopwatch.Elapsed, false);
            _logger.LogErrorWithCorrelation(ex, "Error getting {EntityType} by composite key {CompositeKey}", typeof(T).Name, compositeKey);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets all entities of the specified type.
    /// </summary>
    /// <returns>A collection of all entities.</returns>
    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        using var activity = ActivitySource.StartActivity($"GetAll{typeof(T).Name}");
        activity?.SetTag("entity.type", typeof(T).Name);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await _collection.Find(Builders<T>.Filter.Empty).ToListAsync();

            activity?.SetTag("result.count", result.Count);

            // Record operation metrics
            stopwatch.Stop();
            _metricsService.RecordOperation("getall", typeof(T).Name.ToLowerInvariant(), stopwatch.Elapsed, true);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metricsService.RecordOperation("getall", typeof(T).Name.ToLowerInvariant(), stopwatch.Elapsed, false);
            _logger.LogErrorWithCorrelation(ex, "Error getting all {EntityType}", typeof(T).Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets a paged collection of entities.
    /// </summary>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The number of entities per page.</param>
    /// <returns>A collection of entities for the specified page.</returns>
    public virtual async Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize)
    {
        using var activity = ActivitySource.StartActivity($"GetPaged{typeof(T).Name}");
        activity?.SetTag("entity.type", typeof(T).Name);
        activity?.SetTag("page", page);
        activity?.SetTag("pageSize", pageSize);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var skip = (page - 1) * pageSize;
            var result = await _collection
                .Find(Builders<T>.Filter.Empty)
                .Skip(skip)
                .Limit(pageSize)
                .ToListAsync();

            activity?.SetTag("result.count", result.Count);

            // Record operation metrics
            stopwatch.Stop();
            _metricsService.RecordOperation("getpaged", typeof(T).Name.ToLowerInvariant(), stopwatch.Elapsed, true);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metricsService.RecordOperation("getpaged", typeof(T).Name.ToLowerInvariant(), stopwatch.Elapsed, false);
            _logger.LogErrorWithCorrelation(ex, "Error getting paged {EntityType} (page: {Page}, size: {PageSize})", typeof(T).Name, page, pageSize);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Creates a new entity.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    /// <returns>The created entity with generated ID and timestamps.</returns>
    public virtual async Task<T> CreateAsync(T entity)
    {
        using var activity = ActivitySource.StartActivity($"Create{typeof(T).Name}");
        activity?.SetTag("entity.type", typeof(T).Name);
        activity?.SetTag("entity.compositeKey", entity.GetCompositeKey());

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Set timestamps - MongoDB will auto-generate the ID
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            // Validate composite key uniqueness BEFORE insertion
            if (await ExistsAsync(entity.GetCompositeKey()))
            {
                throw new DuplicateKeyException($"{typeof(T).Name} with composite key '{entity.GetCompositeKey()}' already exists");
            }

            // MongoDB will auto-generate the GUID ID during insertion
            await _collection.InsertOneAsync(entity);

            activity?.SetTag("entity.id", entity.Id.ToString());
            _logger.LogInformationWithCorrelation("Created {EntityType} with auto-generated ID {Id} and composite key {CompositeKey}",
                typeof(T).Name, entity.Id, entity.GetCompositeKey());

            // Record successful operation metrics
            stopwatch.Stop();
            _metricsService.RecordOperation("create", typeof(T).Name.ToLowerInvariant(), stopwatch.Elapsed, true);

            // Publish created event
            await PublishCreatedEventAsync(entity);

            return entity;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            stopwatch.Stop();
            _metricsService.RecordOperation("create", typeof(T).Name.ToLowerInvariant(), stopwatch.Elapsed, false);
            _logger.LogWarningWithCorrelation("Duplicate key error creating {EntityType}: {Error}", typeof(T).Name, ex.WriteError.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.WriteError.Message);
            throw new DuplicateKeyException($"Duplicate key error: {ex.WriteError.Message}", ex);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metricsService.RecordOperation("create", typeof(T).Name.ToLowerInvariant(), stopwatch.Elapsed, false);
            _logger.LogErrorWithCorrelation(ex, "Error creating {EntityType}", typeof(T).Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <returns>The updated entity.</returns>
    public virtual async Task<T> UpdateAsync(T entity)
    {
        using var activity = ActivitySource.StartActivity($"Update{typeof(T).Name}");
        activity?.SetTag("entity.type", typeof(T).Name);
        activity?.SetTag("entity.id", entity.Id.ToString());
        activity?.SetTag("entity.compositeKey", entity.GetCompositeKey());

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Validate that entity has an ID (not new)
            if (entity.IsNew)
            {
                throw new InvalidOperationException($"Cannot update {typeof(T).Name} with empty ID. Use CreateAsync for new entities.");
            }

            entity.UpdatedAt = DateTime.UtcNow;

            // Check if we're changing the composite key and if the new key already exists
            var existing = await GetByIdAsync(entity.Id);
            if (existing == null)
            {
                throw new EntityNotFoundException($"{typeof(T).Name} with ID {entity.Id} not found");
            }

            // If composite key is changing, validate uniqueness
            if (existing.GetCompositeKey() != entity.GetCompositeKey())
            {
                if (await ExistsAsync(entity.GetCompositeKey()))
                {
                    throw new DuplicateKeyException($"{typeof(T).Name} with composite key '{entity.GetCompositeKey()}' already exists");
                }
            }

            var filter = Builders<T>.Filter.Eq(x => x.Id, entity.Id);
            var result = await _collection.ReplaceOneAsync(filter, entity);

            if (result.MatchedCount == 0)
            {
                throw new EntityNotFoundException($"{typeof(T).Name} with ID {entity.Id} not found");
            }

            _logger.LogInformationWithCorrelation("Updated {EntityType} with ID {Id}", typeof(T).Name, entity.Id);

            // Record successful operation metrics
            stopwatch.Stop();
            _metricsService.RecordOperation("update", typeof(T).Name.ToLowerInvariant(), stopwatch.Elapsed, true);

            // Publish updated event
            await PublishUpdatedEventAsync(entity);

            return entity;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metricsService.RecordOperation("update", typeof(T).Name.ToLowerInvariant(), stopwatch.Elapsed, false);
            _logger.LogErrorWithCorrelation(ex, "Error updating {EntityType} with ID {Id}", typeof(T).Name, entity.Id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Deletes an entity by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete.</param>
    /// <returns>True if the entity was deleted, false if not found.</returns>
    public virtual async Task<bool> DeleteAsync(Guid id)
    {
        using var activity = ActivitySource.StartActivity($"Delete{typeof(T).Name}");
        activity?.SetTag("entity.type", typeof(T).Name);
        activity?.SetTag("entity.id", id.ToString());

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var filter = Builders<T>.Filter.Eq(x => x.Id, id);
            var result = await _collection.DeleteOneAsync(filter);

            var deleted = result.DeletedCount > 0;
            activity?.SetTag("result.deleted", deleted);
            _logger.LogInformationWithCorrelation("Deleted {EntityType} with ID {Id}: {Success}", typeof(T).Name, id, deleted);

            // Record operation metrics (success if entity was found and deleted)
            stopwatch.Stop();
            _metricsService.RecordOperation("delete", typeof(T).Name.ToLowerInvariant(), stopwatch.Elapsed, deleted);

            // Publish deleted event if entity was actually deleted
            if (deleted)
            {
                await PublishDeletedEventAsync(id, "System"); // TODO: Get actual user context
            }

            return deleted;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metricsService.RecordOperation("delete", typeof(T).Name.ToLowerInvariant(), stopwatch.Elapsed, false);
            _logger.LogErrorWithCorrelation(ex, "Error deleting {EntityType} with ID {Id}", typeof(T).Name, id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Checks if an entity exists with the specified composite key.
    /// </summary>
    /// <param name="compositeKey">The composite key to check.</param>
    /// <returns>True if an entity exists with the composite key, otherwise false.</returns>
    public virtual async Task<bool> ExistsAsync(string compositeKey)
    {
        try
        {
            var filter = CreateCompositeKeyFilter(compositeKey);
            var count = await _collection.CountDocumentsAsync(filter, new CountOptions { Limit = 1 });
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error checking existence of {EntityType} with composite key {CompositeKey}", typeof(T).Name, compositeKey);
            throw;
        }
    }

    /// <summary>
    /// Checks if an entity exists with the specified unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier to check.</param>
    /// <returns>True if an entity exists with the ID, otherwise false.</returns>
    public virtual async Task<bool> ExistsByIdAsync(Guid id)
    {
        try
        {
            var filter = Builders<T>.Filter.Eq(x => x.Id, id);
            var count = await _collection.CountDocumentsAsync(filter, new CountOptions { Limit = 1 });
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error checking existence of {EntityType} with ID {Id}", typeof(T).Name, id);
            throw;
        }
    }

    /// <summary>
    /// Gets the total count of entities.
    /// </summary>
    /// <returns>The total number of entities.</returns>
    public virtual async Task<long> CountAsync()
    {
        try
        {
            return await _collection.CountDocumentsAsync(Builders<T>.Filter.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithCorrelation(ex, "Error counting {EntityType}", typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// Creates a filter definition for finding entities by composite key.
    /// Must be implemented by derived classes to define how composite keys are matched.
    /// </summary>
    /// <param name="compositeKey">The composite key to create a filter for.</param>
    /// <returns>A filter definition for the composite key.</returns>
    protected abstract FilterDefinition<T> CreateCompositeKeyFilter(string compositeKey);

    /// <summary>
    /// Creates database indexes for the entity collection.
    /// Should be implemented by derived classes to define appropriate indexes.
    /// </summary>
    protected abstract void CreateIndexes();

    /// <summary>
    /// Publishes a domain event when an entity is created.
    /// Must be implemented by derived classes to publish appropriate events.
    /// </summary>
    /// <param name="entity">The created entity.</param>
    protected abstract Task PublishCreatedEventAsync(T entity);

    /// <summary>
    /// Publishes a domain event when an entity is updated.
    /// Must be implemented by derived classes to publish appropriate events.
    /// </summary>
    /// <param name="entity">The updated entity.</param>
    protected abstract Task PublishUpdatedEventAsync(T entity);

    /// <summary>
    /// Publishes a domain event when an entity is deleted.
    /// Must be implemented by derived classes to publish appropriate events.
    /// </summary>
    /// <param name="id">The ID of the deleted entity.</param>
    /// <param name="deletedBy">The user who deleted the entity.</param>
    protected abstract Task PublishDeletedEventAsync(Guid id, string deletedBy);
}
