using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Shared.Entities;
using Shared.Entities.Base;

namespace Shared.MongoDB;

/// <summary>
/// Custom serializer for JsonElement to handle JSON objects in MongoDB.
/// Provides bidirectional conversion between System.Text.Json.JsonElement and BSON types.
/// </summary>
public class JsonElementSerializer : SerializerBase<JsonElement>
{
    /// <summary>
    /// Deserializes a BSON value to a JsonElement.
    /// </summary>
    /// <param name="context">The deserialization context.</param>
    /// <param name="args">The deserialization arguments.</param>
    /// <returns>A JsonElement representing the BSON value.</returns>
    public override JsonElement Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var bsonReader = context.Reader;
        var bsonType = bsonReader.GetCurrentBsonType();

        switch (bsonType)
        {
            case BsonType.Document:
                var document = BsonDocumentSerializer.Instance.Deserialize(context, args);
                var json = document.ToJson();
                return JsonDocument.Parse(json).RootElement;
            case BsonType.Array:
                var array = BsonArraySerializer.Instance.Deserialize(context, args);
                var arrayJson = array.ToJson();
                return JsonDocument.Parse(arrayJson).RootElement;
            case BsonType.String:
                var stringValue = bsonReader.ReadString();
                return JsonDocument.Parse($"\"{stringValue}\"").RootElement;
            case BsonType.Int32:
                var intValue = bsonReader.ReadInt32();
                return JsonDocument.Parse(intValue.ToString()).RootElement;
            case BsonType.Int64:
                var longValue = bsonReader.ReadInt64();
                return JsonDocument.Parse(longValue.ToString()).RootElement;
            case BsonType.Double:
                var doubleValue = bsonReader.ReadDouble();
                return JsonDocument.Parse(doubleValue.ToString()).RootElement;
            case BsonType.Boolean:
                var boolValue = bsonReader.ReadBoolean();
                return JsonDocument.Parse(boolValue.ToString().ToLower()).RootElement;
            case BsonType.Null:
                bsonReader.ReadNull();
                return JsonDocument.Parse("null").RootElement;
            default:
                throw new BsonSerializationException($"Cannot deserialize BsonType {bsonType} to JsonElement");
        }
    }

    /// <summary>
    /// Serializes a JsonElement to BSON.
    /// </summary>
    /// <param name="context">The serialization context.</param>
    /// <param name="args">The serialization arguments.</param>
    /// <param name="value">The JsonElement to serialize.</param>
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, JsonElement value)
    {
        var bsonWriter = context.Writer;

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var document = BsonDocument.Parse(value.GetRawText());
                BsonDocumentSerializer.Instance.Serialize(context, document);
                break;
            case JsonValueKind.Array:
                var arrayDocument = BsonDocument.Parse($"{{\"array\":{value.GetRawText()}}}");
                var array = arrayDocument["array"].AsBsonArray;
                BsonArraySerializer.Instance.Serialize(context, array);
                break;
            case JsonValueKind.String:
                bsonWriter.WriteString(value.GetString());
                break;
            case JsonValueKind.Number:
                if (value.TryGetInt32(out var intVal))
                    bsonWriter.WriteInt32(intVal);
                else if (value.TryGetInt64(out var longVal))
                    bsonWriter.WriteInt64(longVal);
                else
                    bsonWriter.WriteDouble(value.GetDouble());
                break;
            case JsonValueKind.True:
                bsonWriter.WriteBoolean(true);
                break;
            case JsonValueKind.False:
                bsonWriter.WriteBoolean(false);
                break;
            case JsonValueKind.Null:
                bsonWriter.WriteNull();
                break;
            default:
                throw new BsonSerializationException($"Cannot serialize JsonValueKind {value.ValueKind} to BSON");
        }
    }
}

/// <summary>
/// Provides configuration for BSON serialization in MongoDB.
/// Configures serializers, ID generators, and class maps for entities.
/// </summary>
public static class BsonConfiguration
{
    private static bool _isConfigured = false;
    private static readonly object _lock = new object();

    /// <summary>
    /// Configures BSON serialization settings for MongoDB.
    /// This method is thread-safe and will only configure settings once.
    /// </summary>
    public static void Configure()
    {
        if (_isConfigured) return;

        lock (_lock)
        {
            if (_isConfigured) return;

            // Register custom GUID generator for auto-generation
            BsonSerializer.RegisterIdGenerator(typeof(Guid), new GuidGenerator());

            // Configure GUID serialization as string
            BsonSerializer.RegisterSerializer(new GuidSerializer(BsonType.String));

            // Register JsonElement serializer for handling JSON objects in Configuration
            BsonSerializer.RegisterSerializer(typeof(JsonElement), new JsonElementSerializer());

            // Register object serializer that can handle JsonElement
            var objectSerializer = new ObjectSerializer(type => type == typeof(JsonElement) || ObjectSerializer.DefaultAllowedTypes(type));
            BsonSerializer.RegisterSerializer(typeof(object), objectSerializer);

            // Register class maps for entities to ensure proper ID generation
            if (!BsonClassMap.IsClassMapRegistered(typeof(BaseEntity)))
            {
                BsonClassMap.RegisterClassMap<BaseEntity>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdProperty(x => x.Id)
                      .SetIdGenerator(new GuidGenerator())
                      .SetSerializer(new GuidSerializer(BsonType.String));
                    cm.SetIgnoreExtraElements(true);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(SchemaEntity)))
            {
                BsonClassMap.RegisterClassMap<SchemaEntity>(cm =>
                {
                    cm.AutoMap();
                    cm.SetIgnoreExtraElements(true);
                });
            }

            _isConfigured = true;
        }
    }
}
