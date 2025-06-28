using MongoDB.Driver;
using Shared.Entities;
using Shared.MassTransit.Events;
using Shared.Repositories.Base;
using Shared.Services;

namespace Manager.Schema.Repositories;

public class SchemaEntityRepository : BaseRepository<SchemaEntity>, ISchemaEntityRepository
{
    public SchemaEntityRepository(
        IMongoDatabase database,
        ILogger<BaseRepository<SchemaEntity>> logger,
        IEventPublisher eventPublisher,
        IManagerMetricsService metricsService)
        : base(database, "Schema", logger, eventPublisher, metricsService)
    {
    }

    public async Task<IEnumerable<SchemaEntity>> GetByVersionAsync(string version)
    {
        var filter = Builders<SchemaEntity>.Filter.Eq(x => x.Version, version);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<SchemaEntity>> GetByNameAsync(string name)
    {
        var filter = Builders<SchemaEntity>.Filter.Eq(x => x.Name, name);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<SchemaEntity>> GetByDefinitionAsync(string definition)
    {
        var filter = Builders<SchemaEntity>.Filter.Eq(x => x.Definition, definition);
        return await _collection.Find(filter).ToListAsync();
    }

    protected override void CreateIndexes()
    {
        // Call base implementation if it exists, but since it's abstract, we implement it here

        // Create compound index for composite key (version + name)
        var compositeKeyIndex = Builders<SchemaEntity>.IndexKeys
            .Ascending(x => x.Version)
            .Ascending(x => x.Name);
        
        _collection.Indexes.CreateOne(new CreateIndexModel<SchemaEntity>(
            compositeKeyIndex, 
            new CreateIndexOptions { Unique = true, Name = "idx_version_name_unique" }));

        // Create individual indexes for search operations
        var versionIndex = Builders<SchemaEntity>.IndexKeys.Ascending(x => x.Version);
        _collection.Indexes.CreateOne(new CreateIndexModel<SchemaEntity>(
            versionIndex, 
            new CreateIndexOptions { Name = "idx_version" }));

        var nameIndex = Builders<SchemaEntity>.IndexKeys.Ascending(x => x.Name);
        _collection.Indexes.CreateOne(new CreateIndexModel<SchemaEntity>(
            nameIndex, 
            new CreateIndexOptions { Name = "idx_name" }));

        var definitionIndex = Builders<SchemaEntity>.IndexKeys.Ascending(x => x.Definition);
        _collection.Indexes.CreateOne(new CreateIndexModel<SchemaEntity>(
            definitionIndex,
            new CreateIndexOptions { Name = "idx_definition" }));
    }

    protected override FilterDefinition<SchemaEntity> CreateCompositeKeyFilter(string compositeKey)
    {
        // SchemaEntity composite key format: "version_name"
        var parts = compositeKey.Split('_', 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid composite key format: {compositeKey}. Expected format: 'version_name'");
        }

        var version = parts[0];
        var name = parts[1];

        return Builders<SchemaEntity>.Filter.And(
            Builders<SchemaEntity>.Filter.Eq(x => x.Version, version),
            Builders<SchemaEntity>.Filter.Eq(x => x.Name, name)
        );
    }

    protected override async Task PublishCreatedEventAsync(SchemaEntity entity)
    {
        var createdEvent = new SchemaCreatedEvent
        {
            Id = entity.Id,
            Version = entity.Version,
            Name = entity.Name,
            Description = entity.Description,
            Definition = entity.Definition,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy
        };

        await _eventPublisher.PublishAsync(createdEvent);
    }

    protected override async Task PublishUpdatedEventAsync(SchemaEntity entity)
    {
        var updatedEvent = new SchemaUpdatedEvent
        {
            Id = entity.Id,
            Version = entity.Version,
            Name = entity.Name,
            Description = entity.Description,
            Definition = entity.Definition,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };

        await _eventPublisher.PublishAsync(updatedEvent);
    }

    protected override async Task PublishDeletedEventAsync(Guid id, string deletedBy)
    {
        var deletedEvent = new SchemaDeletedEvent
        {
            Id = id,
            DeletedAt = DateTime.UtcNow,
            DeletedBy = deletedBy
        };

        await _eventPublisher.PublishAsync(deletedEvent);
    }
}
