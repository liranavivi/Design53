using MongoDB.Driver;
using Shared.Entities;
using Shared.MassTransit.Events;
using Shared.Repositories.Base;
using Shared.Services;

namespace Manager.Workflow.Repositories;

public class WorkflowEntityRepository : BaseRepository<WorkflowEntity>, IWorkflowEntityRepository
{
    public WorkflowEntityRepository(
        IMongoDatabase database,
        ILogger<BaseRepository<WorkflowEntity>> logger,
        IEventPublisher eventPublisher,
        IManagerMetricsService metricsService)
        : base(database, "Workflow", logger, eventPublisher, metricsService)
    {
    }

    public async Task<IEnumerable<WorkflowEntity>> GetByVersionAsync(string version)
    {
        var filter = Builders<WorkflowEntity>.Filter.Eq(x => x.Version, version);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<WorkflowEntity>> GetByNameAsync(string name)
    {
        var filter = Builders<WorkflowEntity>.Filter.Eq(x => x.Name, name);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<WorkflowEntity>> GetByStepIdAsync(Guid stepId)
    {
        var filter = Builders<WorkflowEntity>.Filter.AnyEq(x => x.StepIds, stepId);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<WorkflowEntity>> GetByStepIdsAsync(List<Guid> stepIds)
    {
        var filter = Builders<WorkflowEntity>.Filter.AnyIn(x => x.StepIds, stepIds);
        return await _collection.Find(filter).ToListAsync();
    }

    protected override void CreateIndexes()
    {
        // Call base implementation if it exists, but since it's abstract, we implement it here

        // Create compound index for composite key (version + name)
        var compositeKeyIndex = Builders<WorkflowEntity>.IndexKeys
            .Ascending(x => x.Version)
            .Ascending(x => x.Name);
        
        _collection.Indexes.CreateOne(new CreateIndexModel<WorkflowEntity>(
            compositeKeyIndex, 
            new CreateIndexOptions { Unique = true, Name = "idx_version_name_unique" }));

        // Create individual indexes for search operations
        var versionIndex = Builders<WorkflowEntity>.IndexKeys.Ascending(x => x.Version);
        _collection.Indexes.CreateOne(new CreateIndexModel<WorkflowEntity>(
            versionIndex, 
            new CreateIndexOptions { Name = "idx_version" }));

        var nameIndex = Builders<WorkflowEntity>.IndexKeys.Ascending(x => x.Name);
        _collection.Indexes.CreateOne(new CreateIndexModel<WorkflowEntity>(
            nameIndex,
            new CreateIndexOptions { Name = "idx_name" }));

        var stepIdsIndex = Builders<WorkflowEntity>.IndexKeys.Ascending(x => x.StepIds);
        _collection.Indexes.CreateOne(new CreateIndexModel<WorkflowEntity>(
            stepIdsIndex,
            new CreateIndexOptions { Name = "idx_stepIds" }));
    }

    protected override FilterDefinition<WorkflowEntity> CreateCompositeKeyFilter(string compositeKey)
    {
        // WorkflowEntity composite key format: "version_name"
        var parts = compositeKey.Split('_', 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid composite key format: {compositeKey}. Expected format: 'version_name'");
        }

        var version = parts[0];
        var name = parts[1];

        return Builders<WorkflowEntity>.Filter.And(
            Builders<WorkflowEntity>.Filter.Eq(x => x.Version, version),
            Builders<WorkflowEntity>.Filter.Eq(x => x.Name, name)
        );
    }

    protected override async Task PublishCreatedEventAsync(WorkflowEntity entity)
    {
        var createdEvent = new WorkflowCreatedEvent
        {
            Id = entity.Id,
            Version = entity.Version,
            Name = entity.Name,
            Description = entity.Description,
            StepIds = entity.StepIds,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy
        };

        await _eventPublisher.PublishAsync(createdEvent);
    }

    protected override async Task PublishUpdatedEventAsync(WorkflowEntity entity)
    {
        var updatedEvent = new WorkflowUpdatedEvent
        {
            Id = entity.Id,
            Version = entity.Version,
            Name = entity.Name,
            Description = entity.Description,
            StepIds = entity.StepIds,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };

        await _eventPublisher.PublishAsync(updatedEvent);
    }

    protected override async Task PublishDeletedEventAsync(Guid id, string deletedBy)
    {
        var deletedEvent = new WorkflowDeletedEvent
        {
            Id = id,
            DeletedAt = DateTime.UtcNow,
            DeletedBy = deletedBy
        };

        await _eventPublisher.PublishAsync(deletedEvent);
    }
}
