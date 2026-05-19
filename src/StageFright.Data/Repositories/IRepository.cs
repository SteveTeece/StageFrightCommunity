using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace StageFright.Data.Repositories;

/// <summary>
/// Base repository interface defining CRUD operations for all entities.
/// Supports soft-delete pattern and generic queries.
/// </summary>
public interface IRepository<TEntity> where TEntity : class
{
	/// <summary>Gets an entity by its ID (excluding soft-deleted records).</summary>
	Task<TEntity?> GetByIdAsync(Guid id);

	/// <summary>Gets all entities (excluding soft-deleted records).</summary>
	Task<IEnumerable<TEntity>> GetAllAsync();

	/// <summary>Finds entities matching a predicate.</summary>
	Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate);

	/// <summary>Creates a new entity.</summary>
	Task CreateAsync(TEntity entity);

	/// <summary>Updates an existing entity.</summary>
	Task UpdateAsync(TEntity entity);

	/// <summary>Soft-deletes an entity (if supported).</summary>
	Task SoftDeleteAsync(Guid id, string? deletedBy = null);

	/// <summary>Restores a soft-deleted entity (if supported).</summary>
	Task RestoreAsync(Guid id);

	/// <summary>Saves changes to the database.</summary>
	Task SaveChangesAsync();
}
