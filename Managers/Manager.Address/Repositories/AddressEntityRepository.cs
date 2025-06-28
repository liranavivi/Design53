using MongoDB.Driver;
using Shared.Entities;
using Shared.MassTransit.Events;
using Shared.Repositories.Base;
using Shared.Services;

namespace Manager.Address.Repositories;

public class AddressEntityRepository : BaseRepository<AddressEntity>, IAddressEntityRepository
{
    public AddressEntityRepository(
        IMongoDatabase database,
        ILogger<BaseRepository<AddressEntity>> logger,
        IEventPublisher eventPublisher,
        IManagerMetricsService metricsService)
        : base(database, "Address", logger, eventPublisher, metricsService)
    {
    }

    public async Task<IEnumerable<AddressEntity>> GetByVersionAsync(string version)
    {
        var filter = Builders<AddressEntity>.Filter.Eq(x => x.Version, version);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<AddressEntity>> GetByNameAsync(string name)
    {
        var filter = Builders<AddressEntity>.Filter.Eq(x => x.Name, name);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<AddressEntity>> GetByConnectionStringAsync(string connectionString)
    {
        var filter = Builders<AddressEntity>.Filter.Eq(x => x.ConnectionString, connectionString);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<AddressEntity>> GetBySchemaIdAsync(Guid schemaId)
    {
        var filter = Builders<AddressEntity>.Filter.Eq(x => x.SchemaId, schemaId);
        return await _collection.Find(filter).ToListAsync();
    }

    protected override void CreateIndexes()
    {
        // Call base implementation if it exists, but since it's abstract, we implement it here

        // Create index for composite key (connectionString only)
        var compositeKeyIndex = Builders<AddressEntity>.IndexKeys
            .Ascending(x => x.ConnectionString);

        _collection.Indexes.CreateOne(new CreateIndexModel<AddressEntity>(
            compositeKeyIndex,
            new CreateIndexOptions { Unique = true, Name = "idx_connectionString_unique" }));

        // Create individual indexes for search operations
        var versionIndex = Builders<AddressEntity>.IndexKeys.Ascending(x => x.Version);
        _collection.Indexes.CreateOne(new CreateIndexModel<AddressEntity>(
            versionIndex,
            new CreateIndexOptions { Name = "idx_version" }));

        var nameIndex = Builders<AddressEntity>.IndexKeys.Ascending(x => x.Name);
        _collection.Indexes.CreateOne(new CreateIndexModel<AddressEntity>(
            nameIndex,
            new CreateIndexOptions { Name = "idx_name" }));

        // Note: ConnectionString already has a unique index as part of the composite key

        var schemaIdIndex = Builders<AddressEntity>.IndexKeys.Ascending(x => x.SchemaId);
        _collection.Indexes.CreateOne(new CreateIndexModel<AddressEntity>(
            schemaIdIndex,
            new CreateIndexOptions { Name = "idx_schemaId" }));
    }

    protected override FilterDefinition<AddressEntity> CreateCompositeKeyFilter(string compositeKey)
    {
        // AddressEntity composite key format: "connectionString"
        // The composite key is just the connection string value
        return Builders<AddressEntity>.Filter.Eq(x => x.ConnectionString, compositeKey);
    }

    protected override async Task PublishCreatedEventAsync(AddressEntity entity)
    {
        var createdEvent = new AddressCreatedEvent
        {
            Id = entity.Id,
            Version = entity.Version,
            Name = entity.Name,
            Description = entity.Description,
            ConnectionString = entity.ConnectionString,
            Payload = entity.Payload,
            SchemaId = entity.SchemaId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy
        };

        await _eventPublisher.PublishAsync(createdEvent);
    }

    protected override async Task PublishUpdatedEventAsync(AddressEntity entity)
    {
        var updatedEvent = new AddressUpdatedEvent
        {
            Id = entity.Id,
            Version = entity.Version,
            Name = entity.Name,
            Description = entity.Description,
            ConnectionString = entity.ConnectionString,
            Payload = entity.Payload,
            SchemaId = entity.SchemaId,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };

        await _eventPublisher.PublishAsync(updatedEvent);
    }

    protected override async Task PublishDeletedEventAsync(Guid id, string deletedBy)
    {
        var deletedEvent = new AddressDeletedEvent
        {
            Id = id,
            DeletedAt = DateTime.UtcNow,
            DeletedBy = deletedBy
        };

        await _eventPublisher.PublishAsync(deletedEvent);
    }

    public async Task<bool> HasSchemaReferences(Guid schemaId)
    {
        var filter = Builders<AddressEntity>.Filter.Eq(x => x.SchemaId, schemaId);
        var count = await _collection.CountDocumentsAsync(filter);
        return count > 0;
    }
}
