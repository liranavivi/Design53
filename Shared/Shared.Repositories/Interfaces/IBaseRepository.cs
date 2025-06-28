using Shared.Entities.Base;

namespace Shared.Repositories.Interfaces;

/// <summary>
/// Base repository interface that defines common CRUD operations for all entities.
/// </summary>
/// <typeparam name="T">The entity type that inherits from BaseEntity.</typeparam>
public interface IBaseRepository<T> where T : BaseEntity
{
    /// <summary>
    /// Gets an entity by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity.</param>
    /// <returns>The entity if found, otherwise null.</returns>
    Task<T?> GetByIdAsync(Guid id);

    /// <summary>
    /// Gets an entity by its composite key.
    /// </summary>
    /// <param name="compositeKey">The composite key of the entity.</param>
    /// <returns>The entity if found, otherwise null.</returns>
    Task<T?> GetByCompositeKeyAsync(string compositeKey);

    /// <summary>
    /// Gets all entities of the specified type.
    /// </summary>
    /// <returns>A collection of all entities.</returns>
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// Gets a paged collection of entities.
    /// </summary>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The number of entities per page.</param>
    /// <returns>A collection of entities for the specified page.</returns>
    Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize);

    /// <summary>
    /// Creates a new entity.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    /// <returns>The created entity with generated ID and timestamps.</returns>
    Task<T> CreateAsync(T entity);

    /// <summary>
    /// Updates an existing entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <returns>The updated entity.</returns>
    Task<T> UpdateAsync(T entity);

    /// <summary>
    /// Deletes an entity by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete.</param>
    /// <returns>True if the entity was deleted, false if not found.</returns>
    Task<bool> DeleteAsync(Guid id);

    /// <summary>
    /// Checks if an entity exists with the specified composite key.
    /// </summary>
    /// <param name="compositeKey">The composite key to check.</param>
    /// <returns>True if an entity exists with the composite key, otherwise false.</returns>
    Task<bool> ExistsAsync(string compositeKey);

    /// <summary>
    /// Checks if an entity exists with the specified unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier to check.</param>
    /// <returns>True if an entity exists with the ID, otherwise false.</returns>
    Task<bool> ExistsByIdAsync(Guid id);

    /// <summary>
    /// Gets the total count of entities.
    /// </summary>
    /// <returns>The total number of entities.</returns>
    Task<long> CountAsync();
}
