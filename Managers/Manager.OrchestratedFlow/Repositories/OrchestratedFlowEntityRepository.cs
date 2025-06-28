using MongoDB.Driver;
using Shared.Entities;
using Shared.MassTransit.Events;
using Shared.Repositories.Base;
using Shared.Services;

namespace Manager.OrchestratedFlow.Repositories;

public class OrchestratedFlowEntityRepository : BaseRepository<OrchestratedFlowEntity>, IOrchestratedFlowEntityRepository
{
    public OrchestratedFlowEntityRepository(
        IMongoDatabase database,
        ILogger<BaseRepository<OrchestratedFlowEntity>> logger,
        IEventPublisher eventPublisher,
        IManagerMetricsService metricsService)
        : base(database, "OrchestratedFlow", logger, eventPublisher, metricsService)
    {
    }

    public async Task<IEnumerable<OrchestratedFlowEntity>> GetByVersionAsync(string version)
    {
        var filter = Builders<OrchestratedFlowEntity>.Filter.Eq(x => x.Version, version);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<OrchestratedFlowEntity>> GetByNameAsync(string name)
    {
        var filter = Builders<OrchestratedFlowEntity>.Filter.Eq(x => x.Name, name);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<OrchestratedFlowEntity>> GetByWorkflowIdAsync(Guid workflowId)
    {
        var filter = Builders<OrchestratedFlowEntity>.Filter.Eq(x => x.WorkflowId, workflowId);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<OrchestratedFlowEntity>> GetByAssignmentIdAsync(Guid assignmentId)
    {
        var filter = Builders<OrchestratedFlowEntity>.Filter.AnyEq(x => x.AssignmentIds, assignmentId);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<bool> HasWorkflowReferences(Guid workflowId)
    {
        var filter = Builders<OrchestratedFlowEntity>.Filter.Eq(x => x.WorkflowId, workflowId);
        var count = await _collection.CountDocumentsAsync(filter);
        return count > 0;
    }

    public async Task<bool> HasAssignmentReferences(Guid assignmentId)
    {
        var filter = Builders<OrchestratedFlowEntity>.Filter.AnyEq(x => x.AssignmentIds, assignmentId);
        var count = await _collection.CountDocumentsAsync(filter);
        return count > 0;
    }

    protected override void CreateIndexes()
    {
        // Call base implementation if it exists, but since it's abstract, we implement it here

        // Create compound index for composite key (version + name)
        var compositeKeyIndex = Builders<OrchestratedFlowEntity>.IndexKeys
            .Ascending(x => x.Version)
            .Ascending(x => x.Name);
        
        _collection.Indexes.CreateOne(new CreateIndexModel<OrchestratedFlowEntity>(
            compositeKeyIndex, 
            new CreateIndexOptions { Unique = true, Name = "idx_version_name_unique" }));

        // Create individual indexes for search operations
        var versionIndex = Builders<OrchestratedFlowEntity>.IndexKeys.Ascending(x => x.Version);
        _collection.Indexes.CreateOne(new CreateIndexModel<OrchestratedFlowEntity>(
            versionIndex, 
            new CreateIndexOptions { Name = "idx_version" }));

        var nameIndex = Builders<OrchestratedFlowEntity>.IndexKeys.Ascending(x => x.Name);
        _collection.Indexes.CreateOne(new CreateIndexModel<OrchestratedFlowEntity>(
            nameIndex, 
            new CreateIndexOptions { Name = "idx_name" }));

        var workflowIdIndex = Builders<OrchestratedFlowEntity>.IndexKeys.Ascending(x => x.WorkflowId);
        _collection.Indexes.CreateOne(new CreateIndexModel<OrchestratedFlowEntity>(
            workflowIdIndex,
            new CreateIndexOptions { Name = "idx_workflowId" }));

        var assignmentIdsIndex = Builders<OrchestratedFlowEntity>.IndexKeys.Ascending(x => x.AssignmentIds);
        _collection.Indexes.CreateOne(new CreateIndexModel<OrchestratedFlowEntity>(
            assignmentIdsIndex,
            new CreateIndexOptions { Name = "idx_assignmentIds" }));
    }

    protected override FilterDefinition<OrchestratedFlowEntity> CreateCompositeKeyFilter(string compositeKey)
    {
        // OrchestratedFlowEntity composite key format: "version_name"
        var parts = compositeKey.Split('_', 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid composite key format: {compositeKey}. Expected format: 'version_name'");
        }

        var version = parts[0];
        var name = parts[1];

        return Builders<OrchestratedFlowEntity>.Filter.And(
            Builders<OrchestratedFlowEntity>.Filter.Eq(x => x.Version, version),
            Builders<OrchestratedFlowEntity>.Filter.Eq(x => x.Name, name)
        );
    }

    protected override async Task PublishCreatedEventAsync(OrchestratedFlowEntity entity)
    {
        var createdEvent = new OrchestratedFlowCreatedEvent
        {
            Id = entity.Id,
            Version = entity.Version,
            Name = entity.Name,
            Description = entity.Description,
            WorkflowId = entity.WorkflowId,
            AssignmentIds = entity.AssignmentIds,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy
        };

        await _eventPublisher.PublishAsync(createdEvent);
    }

    protected override async Task PublishUpdatedEventAsync(OrchestratedFlowEntity entity)
    {
        var updatedEvent = new OrchestratedFlowUpdatedEvent
        {
            Id = entity.Id,
            Version = entity.Version,
            Name = entity.Name,
            Description = entity.Description,
            WorkflowId = entity.WorkflowId,
            AssignmentIds = entity.AssignmentIds,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };

        await _eventPublisher.PublishAsync(updatedEvent);
    }

    protected override async Task PublishDeletedEventAsync(Guid id, string deletedBy)
    {
        var deletedEvent = new OrchestratedFlowDeletedEvent
        {
            Id = id,
            DeletedAt = DateTime.UtcNow,
            DeletedBy = deletedBy
        };

        await _eventPublisher.PublishAsync(deletedEvent);
    }
}
