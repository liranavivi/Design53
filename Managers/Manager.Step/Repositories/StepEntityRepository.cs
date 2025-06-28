using MongoDB.Driver;
using Shared.Entities;
using Shared.MassTransit.Events;
using Shared.Repositories.Base;
using Shared.Services;

namespace Manager.Step.Repositories;

public class StepEntityRepository : BaseRepository<StepEntity>, IStepEntityRepository
{
    public StepEntityRepository(
        IMongoDatabase database,
        ILogger<BaseRepository<StepEntity>> logger,
        IEventPublisher eventPublisher,
        IManagerMetricsService metricsService)
        : base(database, "Step", logger, eventPublisher, metricsService)
    {
    }

    public async Task<IEnumerable<StepEntity>> GetByVersionAsync(string version)
    {
        var filter = Builders<StepEntity>.Filter.Eq(x => x.Version, version);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<StepEntity>> GetByNameAsync(string name)
    {
        var filter = Builders<StepEntity>.Filter.Eq(x => x.Name, name);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<StepEntity>> GetByProcessorIdAsync(Guid processorId)
    {
        var filter = Builders<StepEntity>.Filter.Eq(x => x.ProcessorId, processorId);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<StepEntity>> GetByNextStepIdAsync(Guid stepId)
    {
        var filter = Builders<StepEntity>.Filter.AnyEq(x => x.NextStepIds, stepId);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<StepEntity>> GetByEntryConditionAsync(Shared.Entities.Enums.StepEntryCondition entryCondition)
    {
        var filter = Builders<StepEntity>.Filter.Eq(x => x.EntryCondition, entryCondition);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<bool> ExistsAsync(Guid stepId)
    {
        var filter = Builders<StepEntity>.Filter.Eq(x => x.Id, stepId);
        var count = await _collection.CountDocumentsAsync(filter);
        return count > 0;
    }

    protected override void CreateIndexes()
    {
        // Call base implementation if it exists, but since it's abstract, we implement it here

        // Create compound index for composite key (version + name)
        var compositeKeyIndex = Builders<StepEntity>.IndexKeys
            .Ascending(x => x.Version)
            .Ascending(x => x.Name);

        _collection.Indexes.CreateOne(new CreateIndexModel<StepEntity>(
            compositeKeyIndex,
            new CreateIndexOptions { Unique = true, Name = "idx_version_name_unique" }));

        // Create individual indexes for search operations
        var versionIndex = Builders<StepEntity>.IndexKeys.Ascending(x => x.Version);
        _collection.Indexes.CreateOne(new CreateIndexModel<StepEntity>(
            versionIndex,
            new CreateIndexOptions { Name = "idx_version" }));

        var nameIndex = Builders<StepEntity>.IndexKeys.Ascending(x => x.Name);
        _collection.Indexes.CreateOne(new CreateIndexModel<StepEntity>(
            nameIndex,
            new CreateIndexOptions { Name = "idx_name" }));

        // Create index for ProcessorId for efficient processor-based queries
        var processorIdIndex = Builders<StepEntity>.IndexKeys.Ascending(x => x.ProcessorId);
        _collection.Indexes.CreateOne(new CreateIndexModel<StepEntity>(
            processorIdIndex,
            new CreateIndexOptions { Name = "idx_processorId" }));

        // Create index for NextStepIds for efficient step relationship queries
        var nextStepIdsIndex = Builders<StepEntity>.IndexKeys.Ascending(x => x.NextStepIds);
        _collection.Indexes.CreateOne(new CreateIndexModel<StepEntity>(
            nextStepIdsIndex,
            new CreateIndexOptions { Name = "idx_nextStepIds" }));

        // Create index for EntryCondition for efficient condition-based queries
        var entryConditionIndex = Builders<StepEntity>.IndexKeys.Ascending(x => x.EntryCondition);
        _collection.Indexes.CreateOne(new CreateIndexModel<StepEntity>(
            entryConditionIndex,
            new CreateIndexOptions { Name = "idx_entryCondition" }));
    }

    protected override FilterDefinition<StepEntity> CreateCompositeKeyFilter(string compositeKey)
    {
        // StepEntity composite key format: "version_name"
        var parts = compositeKey.Split('_', 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid composite key format: {compositeKey}. Expected format: 'version_name'");
        }

        var version = parts[0];
        var name = parts[1];

        return Builders<StepEntity>.Filter.And(
            Builders<StepEntity>.Filter.Eq(x => x.Version, version),
            Builders<StepEntity>.Filter.Eq(x => x.Name, name)
        );
    }

    protected override async Task PublishCreatedEventAsync(StepEntity entity)
    {
        var createdEvent = new StepCreatedEvent
        {
            Id = entity.Id,
            Version = entity.Version,
            Name = entity.Name,
            Description = entity.Description,
            ProcessorId = entity.ProcessorId,
            NextStepIds = entity.NextStepIds,
            EntryCondition = entity.EntryCondition,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy
        };

        await _eventPublisher.PublishAsync(createdEvent);
    }

    protected override async Task PublishUpdatedEventAsync(StepEntity entity)
    {
        var updatedEvent = new StepUpdatedEvent
        {
            Id = entity.Id,
            Version = entity.Version,
            Name = entity.Name,
            Description = entity.Description,
            ProcessorId = entity.ProcessorId,
            NextStepIds = entity.NextStepIds,
            EntryCondition = entity.EntryCondition,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };

        await _eventPublisher.PublishAsync(updatedEvent);
    }

    protected override async Task PublishDeletedEventAsync(Guid id, string deletedBy)
    {
        var deletedEvent = new StepDeletedEvent
        {
            Id = id,
            DeletedAt = DateTime.UtcNow,
            DeletedBy = deletedBy
        };

        await _eventPublisher.PublishAsync(deletedEvent);
    }
}
