using MongoDB.Driver;
using Shared.Entities;
using Shared.MassTransit.Events;
using Shared.Repositories.Base;
using Shared.Services;

namespace Manager.Delivery.Repositories;

public class DeliveryEntityRepository : BaseRepository<DeliveryEntity>, IDeliveryEntityRepository
{
    public DeliveryEntityRepository(
        IMongoDatabase database,
        ILogger<BaseRepository<DeliveryEntity>> logger,
        IEventPublisher eventPublisher,
        IManagerMetricsService metricsService)
        : base(database, "Delivery", logger, eventPublisher, metricsService)
    {
    }

    public async Task<IEnumerable<DeliveryEntity>> GetByVersionAsync(string version)
    {
        var filter = Builders<DeliveryEntity>.Filter.Eq(x => x.Version, version);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<DeliveryEntity>> GetByNameAsync(string name)
    {
        var filter = Builders<DeliveryEntity>.Filter.Eq(x => x.Name, name);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<DeliveryEntity>> GetByPayloadAsync(string payload)
    {
        var filter = Builders<DeliveryEntity>.Filter.Eq(x => x.Payload, payload);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<DeliveryEntity>> GetBySchemaIdAsync(Guid schemaId)
    {
        var filter = Builders<DeliveryEntity>.Filter.Eq(x => x.SchemaId, schemaId);
        return await _collection.Find(filter).ToListAsync();
    }

    protected override void CreateIndexes()
    {
        // Call base implementation if it exists, but since it's abstract, we implement it here

        // Create compound index for composite key (version + name)
        var compositeKeyIndex = Builders<DeliveryEntity>.IndexKeys
            .Ascending(x => x.Version)
            .Ascending(x => x.Name);
        
        _collection.Indexes.CreateOne(new CreateIndexModel<DeliveryEntity>(
            compositeKeyIndex, 
            new CreateIndexOptions { Unique = true, Name = "idx_version_name_unique" }));

        // Create individual indexes for search operations
        var versionIndex = Builders<DeliveryEntity>.IndexKeys.Ascending(x => x.Version);
        _collection.Indexes.CreateOne(new CreateIndexModel<DeliveryEntity>(
            versionIndex, 
            new CreateIndexOptions { Name = "idx_version" }));

        var nameIndex = Builders<DeliveryEntity>.IndexKeys.Ascending(x => x.Name);
        _collection.Indexes.CreateOne(new CreateIndexModel<DeliveryEntity>(
            nameIndex, 
            new CreateIndexOptions { Name = "idx_name" }));

        var payloadIndex = Builders<DeliveryEntity>.IndexKeys.Ascending(x => x.Payload);
        _collection.Indexes.CreateOne(new CreateIndexModel<DeliveryEntity>(
            payloadIndex,
            new CreateIndexOptions { Name = "idx_payload" }));

        var schemaIdIndex = Builders<DeliveryEntity>.IndexKeys.Ascending(x => x.SchemaId);
        _collection.Indexes.CreateOne(new CreateIndexModel<DeliveryEntity>(
            schemaIdIndex,
            new CreateIndexOptions { Name = "idx_schemaId" }));
    }

    protected override FilterDefinition<DeliveryEntity> CreateCompositeKeyFilter(string compositeKey)
    {
        // DeliveryEntity composite key format: "version_name"
        var parts = compositeKey.Split('_', 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid composite key format: {compositeKey}. Expected format: 'version_name'");
        }

        var version = parts[0];
        var name = parts[1];

        return Builders<DeliveryEntity>.Filter.And(
            Builders<DeliveryEntity>.Filter.Eq(x => x.Version, version),
            Builders<DeliveryEntity>.Filter.Eq(x => x.Name, name)
        );
    }

    protected override async Task PublishCreatedEventAsync(DeliveryEntity entity)
    {
        var createdEvent = new DeliveryCreatedEvent
        {
            Id = entity.Id,
            Version = entity.Version,
            Name = entity.Name,
            Description = entity.Description,
            Payload = entity.Payload,
            SchemaId = entity.SchemaId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy
        };

        await _eventPublisher.PublishAsync(createdEvent);
    }

    protected override async Task PublishUpdatedEventAsync(DeliveryEntity entity)
    {
        var updatedEvent = new DeliveryUpdatedEvent
        {
            Id = entity.Id,
            Version = entity.Version,
            Name = entity.Name,
            Description = entity.Description,
            Payload = entity.Payload,
            SchemaId = entity.SchemaId,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };

        await _eventPublisher.PublishAsync(updatedEvent);
    }

    protected override async Task PublishDeletedEventAsync(Guid id, string deletedBy)
    {
        var deletedEvent = new DeliveryDeletedEvent
        {
            Id = id,
            DeletedAt = DateTime.UtcNow,
            DeletedBy = deletedBy
        };

        await _eventPublisher.PublishAsync(deletedEvent);
    }

    public async Task<bool> HasSchemaReferences(Guid schemaId)
    {
        var filter = Builders<DeliveryEntity>.Filter.Eq(x => x.SchemaId, schemaId);
        var count = await _collection.CountDocumentsAsync(filter);
        return count > 0;
    }
}
