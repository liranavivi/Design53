using MongoDB.Bson.Serialization;

namespace Shared.MongoDB;

/// <summary>
/// Custom GUID generator for MongoDB entity IDs.
/// Generates new GUIDs for entity creation and provides empty ID validation.
/// </summary>
public class GuidGenerator : IIdGenerator
{
    /// <summary>
    /// Generates a new GUID for an entity ID.
    /// </summary>
    /// <param name="container">The container object (not used).</param>
    /// <param name="document">The document being created (not used).</param>
    /// <returns>A new GUID.</returns>
    public object GenerateId(object container, object document)
    {
        return Guid.NewGuid();
    }
    
    /// <summary>
    /// Determines whether the provided ID is empty.
    /// </summary>
    /// <param name="id">The ID to check.</param>
    /// <returns>True if the ID is null or an empty GUID; otherwise, false.</returns>
    public bool IsEmpty(object id)
    {
        return id == null || (Guid)id == Guid.Empty;
    }
}
