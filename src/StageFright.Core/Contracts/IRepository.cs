namespace StageFright.Core.Contracts;

/// <summary>
/// Base CRUD contract for all domain entities.
/// GetAllAsync applies soft-delete global query filters where applicable (IsDeleted=false).
/// Infrastructure exceptions are translated to domain exceptions before leaving the DAL.
/// </summary>
public interface IRepository<TEntity> where TEntity : class
{
    /// <summary>Returns the entity with the given primary key, or null if not found.</summary>
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all non-deleted entities (soft-delete filter applied where applicable).</summary>
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Persists a new entity and returns the tracked instance.</summary>
    Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);

    /// <summary>Persists changes to an existing entity.</summary>
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
}
