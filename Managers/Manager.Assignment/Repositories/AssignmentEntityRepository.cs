using MongoDB.Driver;
using Shared.Entities;
using Shared.MassTransit.Events;
using Shared.Repositories.Base;
using Shared.Services;

namespace Manager.Assignment.Repositories;

public class AssignmentEntityRepository : BaseRepository<AssignmentEntity>, IAssignmentEntityRepository
{
    public AssignmentEntityRepository(
        IMongoDatabase database,
        ILogger<BaseRepository<AssignmentEntity>> logger,
        IEventPublisher eventPublisher,
        IManagerMetricsService metricsService)
        : base(database, "Assignment", logger, eventPublisher, metricsService)
    {
    }

    public async Task<IEnumerable<AssignmentEntity>> GetByVersionAsync(string version)
    {
        var filter = Builders<AssignmentEntity>.Filter.Eq(x => x.Version, version);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<AssignmentEntity>> GetByNameAsync(string name)
    {
        var filter = Builders<AssignmentEntity>.Filter.Eq(x => x.Name, name);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<AssignmentEntity>> GetByStepIdAsync(Guid stepId)
    {
        var filter = Builders<AssignmentEntity>.Filter.Eq(x => x.StepId, stepId);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<AssignmentEntity>> GetByEntityIdAsync(Guid entityId)
    {
        var filter = Builders<AssignmentEntity>.Filter.AnyEq(x => x.EntityIds, entityId);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<bool> HasEntityReferences(Guid entityId)
    {
        var filter = Builders<AssignmentEntity>.Filter.AnyEq(x => x.EntityIds, entityId);
        var count = await _collection.CountDocumentsAsync(filter);
        return count > 0;
    }

    protected override void CreateIndexes()
    {
        // Call base implementation if it exists, but since it's abstract, we implement it here

        // Create compound index for composite key (version + name)
        var compositeKeyIndex = Builders<AssignmentEntity>.IndexKeys
            .Ascending(x => x.Version)
            .Ascending(x => x.Name);
        
        _collection.Indexes.CreateOne(new CreateIndexModel<AssignmentEntity>(
            compositeKeyIndex, 
            new CreateIndexOptions { Unique = true, Name = "idx_version_name_unique" }));

        // Create individual indexes for search operations
        var versionIndex = Builders<AssignmentEntity>.IndexKeys.Ascending(x => x.Version);
        _collection.Indexes.CreateOne(new CreateIndexModel<AssignmentEntity>(
            versionIndex, 
            new CreateIndexOptions { Name = "idx_version" }));

        var nameIndex = Builders<AssignmentEntity>.IndexKeys.Ascending(x => x.Name);
        _collection.Indexes.CreateOne(new CreateIndexModel<AssignmentEntity>(
            nameIndex, 
            new CreateIndexOptions { Name = "idx_name" }));

        var stepIdIndex = Builders<AssignmentEntity>.IndexKeys.Ascending(x => x.StepId);
        _collection.Indexes.CreateOne(new CreateIndexModel<AssignmentEntity>(
            stepIdIndex,
            new CreateIndexOptions { Name = "idx_stepId" }));

        var entityIdsIndex = Builders<AssignmentEntity>.IndexKeys.Ascending(x => x.EntityIds);
        _collection.Indexes.CreateOne(new CreateIndexModel<AssignmentEntity>(
            entityIdsIndex,
            new CreateIndexOptions { Name = "idx_entityIds" }));
    }

    protected override FilterDefinition<AssignmentEntity> CreateCompositeKeyFilter(string compositeKey)
    {
        // AssignmentEntity composite key format: "version_name"
        var parts = compositeKey.Split('_', 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid composite key format: {compositeKey}. Expected format: 'version_name'");
        }

        var version = parts[0];
        var name = parts[1];

        return Builders<AssignmentEntity>.Filter.And(
            Builders<AssignmentEntity>.Filter.Eq(x => x.Version, version),
            Builders<AssignmentEntity>.Filter.Eq(x => x.Name, name)
        );
    }

    protected override async Task PublishCreatedEventAsync(AssignmentEntity entity)
    {
        var createdEvent = new AssignmentCreatedEvent
        {
            Id = entity.Id,
            Version = entity.Version,
            Name = entity.Name,
            Description = entity.Description,
            StepId = entity.StepId,
            EntityIds = entity.EntityIds,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy
        };

        await _eventPublisher.PublishAsync(createdEvent);
    }

    protected override async Task PublishUpdatedEventAsync(AssignmentEntity entity)
    {
        var updatedEvent = new AssignmentUpdatedEvent
        {
            Id = entity.Id,
            Version = entity.Version,
            Name = entity.Name,
            Description = entity.Description,
            StepId = entity.StepId,
            EntityIds = entity.EntityIds,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };

        await _eventPublisher.PublishAsync(updatedEvent);
    }

    protected override async Task PublishDeletedEventAsync(Guid id, string deletedBy)
    {
        var deletedEvent = new AssignmentDeletedEvent
        {
            Id = id,
            DeletedAt = DateTime.UtcNow,
            DeletedBy = deletedBy
        };

        await _eventPublisher.PublishAsync(deletedEvent);
    }
}
