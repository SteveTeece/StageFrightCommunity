using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StageFright.Data.Context;

namespace StageFright.Data.Repositories;

/// <summary>
/// Base repository implementation supporting soft-delete pattern.
/// Automatically excludes soft-deleted records from queries.
/// </summary>
public abstract class BaseRepository<TEntity> : IRepository<TEntity> where TEntity : class
{
	protected readonly StageFrightContext _context;
	protected readonly DbSet<TEntity> _dbSet;

	protected BaseRepository(StageFrightContext context)
	{
		_context = context ?? throw new ArgumentNullException(nameof(context));
		_dbSet = context.Set<TEntity>();
	}

	public virtual async Task<TEntity?> GetByIdAsync(Guid id)
	{
		return await _dbSet.FindAsync(id);
	}

	public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
	{
		return await _dbSet.ToListAsync();
	}

	public virtual async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate)
	{
		return await _dbSet.Where(predicate).ToListAsync();
	}

	public virtual async Task CreateAsync(TEntity entity)
	{
		if (entity == null)
			throw new ArgumentNullException(nameof(entity));

		_dbSet.Add(entity);
		await SaveChangesAsync();
	}

	public virtual async Task UpdateAsync(TEntity entity)
	{
		if (entity == null)
			throw new ArgumentNullException(nameof(entity));

		_dbSet.Update(entity);
		await SaveChangesAsync();
	}

	public virtual async Task SoftDeleteAsync(Guid id, string? deletedBy = null)
	{
		var entity = await GetByIdAsync(id);
		if (entity == null)
			throw new InvalidOperationException($"Entity with ID {id} not found.");

		// Check if entity has soft-delete fields
		var isDeletedProperty = entity.GetType().GetProperty("IsDeleted");
		var deletedAtProperty = entity.GetType().GetProperty("DeletedAt");
		var deletedByProperty = entity.GetType().GetProperty("DeletedBy");

		if (isDeletedProperty != null)
		{
			isDeletedProperty.SetValue(entity, true);
		}

		if (deletedAtProperty != null)
		{
			deletedAtProperty.SetValue(entity, DateTime.UtcNow);
		}

		if (deletedByProperty != null && !string.IsNullOrEmpty(deletedBy))
		{
			deletedByProperty.SetValue(entity, deletedBy);
		}

		_dbSet.Update(entity);
		await SaveChangesAsync();
	}

	public virtual async Task RestoreAsync(Guid id)
	{
		var entity = await GetByIdAsync(id);
		if (entity == null)
			throw new InvalidOperationException($"Entity with ID {id} not found.");

		// Check if entity has soft-delete fields
		var isDeletedProperty = entity.GetType().GetProperty("IsDeleted");
		var deletedAtProperty = entity.GetType().GetProperty("DeletedAt");
		var deletedByProperty = entity.GetType().GetProperty("DeletedBy");

		if (isDeletedProperty != null)
		{
			isDeletedProperty.SetValue(entity, false);
		}

		if (deletedAtProperty != null)
		{
			deletedAtProperty.SetValue(entity, null);
		}

		if (deletedByProperty != null)
		{
			deletedByProperty.SetValue(entity, null);
		}

		_dbSet.Update(entity);
		await SaveChangesAsync();
	}

	public virtual async Task SaveChangesAsync()
	{
		await _context.SaveChangesAsync();
	}
}
