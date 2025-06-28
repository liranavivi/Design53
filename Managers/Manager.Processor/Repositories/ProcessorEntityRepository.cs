using MongoDB.Driver;
using Shared.Entities;
using Shared.MassTransit.Events;
using Shared.Repositories.Base;
using Shared.Services;

namespace Manager.Processor.Repositories;

public class ProcessorEntityRepository : BaseRepository<ProcessorEntity>, IProcessorEntityRepository
{
    public ProcessorEntityRepository(
        IMongoDatabase database,
        ILogger<BaseRepository<ProcessorEntity>> logger,
        IEventPublisher eventPublisher,
        IManagerMetricsService metricsService)
        : base(database, "Processor", logger, eventPublisher, metricsService)
    {
    }

    public async Task<IEnumerable<ProcessorEntity>> GetByVersionAsync(string version)
    {
        var filter = Builders<ProcessorEntity>.Filter.Eq(x => x.Version, version);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<ProcessorEntity>> GetByNameAsync(string name)
    {
        var filter = Builders<ProcessorEntity>.Filter.Eq(x => x.Name, name);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<ProcessorEntity>> GetByInputSchemaIdAsync(Guid inputSchemaId)
    {
        var filter = Builders<ProcessorEntity>.Filter.Eq(x => x.InputSchemaId, inputSchemaId);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<ProcessorEntity>> GetByOutputSchemaIdAsync(Guid outputSchemaId)
    {
        var filter = Builders<ProcessorEntity>.Filter.Eq(x => x.OutputSchemaId, outputSchemaId);
        return await _collection.Find(filter).ToListAsync();
    }

    protected override void CreateIndexes()
    {
        // Call base implementation if it exists, but since it's abstract, we implement it here

        // Create compound index for composite key (version + name)
        var compositeKeyIndex = Builders<ProcessorEntity>.IndexKeys
            .Ascending(x => x.Version)
            .Ascending(x => x.Name);
        
        _collection.Indexes.CreateOne(new CreateIndexModel<ProcessorEntity>(
            compositeKeyIndex, 
            new CreateIndexOptions { Unique = true, Name = "idx_version_name_unique" }));

        // Create individual indexes for search operations
        var versionIndex = Builders<ProcessorEntity>.IndexKeys.Ascending(x => x.Version);
        _collection.Indexes.CreateOne(new CreateIndexModel<ProcessorEntity>(
            versionIndex, 
            new CreateIndexOptions { Name = "idx_version" }));

        var nameIndex = Builders<ProcessorEntity>.IndexKeys.Ascending(x => x.Name);
        _collection.Indexes.CreateOne(new CreateIndexModel<ProcessorEntity>(
            nameIndex,
            new CreateIndexOptions { Name = "idx_name" }));

        var inputSchemaIdIndex = Builders<ProcessorEntity>.IndexKeys.Ascending(x => x.InputSchemaId);
        _collection.Indexes.CreateOne(new CreateIndexModel<ProcessorEntity>(
            inputSchemaIdIndex,
            new CreateIndexOptions { Name = "idx_input_schema_id" }));

        var outputSchemaIdIndex = Builders<ProcessorEntity>.IndexKeys.Ascending(x => x.OutputSchemaId);
        _collection.Indexes.CreateOne(new CreateIndexModel<ProcessorEntity>(
            outputSchemaIdIndex,
            new CreateIndexOptions { Name = "idx_output_schema_id" }));
    }

    protected override FilterDefinition<ProcessorEntity> CreateCompositeKeyFilter(string compositeKey)
    {
        // ProcessorEntity composite key format: "version_name"
        var parts = compositeKey.Split('_', 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid composite key format: {compositeKey}. Expected format: 'version_name'");
        }

        var version = parts[0];
        var name = parts[1];

        return Builders<ProcessorEntity>.Filter.And(
            Builders<ProcessorEntity>.Filter.Eq(x => x.Version, version),
            Builders<ProcessorEntity>.Filter.Eq(x => x.Name, name)
        );
    }

    protected override async Task PublishCreatedEventAsync(ProcessorEntity entity)
    {
        var createdEvent = new ProcessorCreatedEvent
        {
            Id = entity.Id,
            Version = entity.Version,
            Name = entity.Name,
            Description = entity.Description,
            InputSchemaId = entity.InputSchemaId,
            OutputSchemaId = entity.OutputSchemaId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy
        };

        await _eventPublisher.PublishAsync(createdEvent);
    }

    protected override async Task PublishUpdatedEventAsync(ProcessorEntity entity)
    {
        var updatedEvent = new ProcessorUpdatedEvent
        {
            Id = entity.Id,
            Version = entity.Version,
            Name = entity.Name,
            Description = entity.Description,
            InputSchemaId = entity.InputSchemaId,
            OutputSchemaId = entity.OutputSchemaId,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };

        await _eventPublisher.PublishAsync(updatedEvent);
    }

    protected override async Task PublishDeletedEventAsync(Guid id, string deletedBy)
    {
        var deletedEvent = new ProcessorDeletedEvent
        {
            Id = id,
            DeletedAt = DateTime.UtcNow,
            DeletedBy = deletedBy
        };

        await _eventPublisher.PublishAsync(deletedEvent);
    }

    public async Task<bool> HasInputSchemaReferences(Guid schemaId)
    {
        var filter = Builders<ProcessorEntity>.Filter.Eq(x => x.InputSchemaId, schemaId);
        var count = await _collection.CountDocumentsAsync(filter);
        return count > 0;
    }

    public async Task<bool> HasOutputSchemaReferences(Guid schemaId)
    {
        var filter = Builders<ProcessorEntity>.Filter.Eq(x => x.OutputSchemaId, schemaId);
        var count = await _collection.CountDocumentsAsync(filter);
        return count > 0;
    }
}
