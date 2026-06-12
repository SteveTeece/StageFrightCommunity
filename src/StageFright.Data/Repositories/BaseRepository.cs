using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;

namespace StageFright.Data.Repositories;

/// <summary>
/// Generic IRepository implementation. Translates EF Core exceptions to domain exceptions
/// before they leave the DAL boundary.
/// </summary>
public class BaseRepository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly StageFrightDbContext _db;

    public BaseRepository(StageFrightDbContext db)
    {
        _db = db;
    }

    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            return await _db.Set<TEntity>().FindAsync(new object[] { id }, ct);
        }
        catch (Exception ex) when (ex is not EntityNotFoundException and not DataAccessException)
        {
            throw new DataAccessException(ex.Message, typeof(TEntity).Name, nameof(GetByIdAsync), id, ex);
        }
    }

    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.Set<TEntity>().ToListAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, typeof(TEntity).Name, nameof(GetAllAsync), null, ex);
        }
    }

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
    {
        try
        {
            var entry = await _db.Set<TEntity>().AddAsync(entity, ct);
            await _db.SaveChangesAsync(ct);
            return entry.Entity;
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new DuplicateEntityException($"A {typeof(TEntity).Name} with these values already exists.", typeof(TEntity).Name, nameof(AddAsync), null, ex);
        }
        catch (Exception ex) when (ex is not DuplicateEntityException and not DataAccessException)
        {
            throw new DataAccessException(ex.Message, typeof(TEntity).Name, nameof(AddAsync), null, ex);
        }
    }

    public virtual async Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        try
        {
            _db.Set<TEntity>().Update(entity);
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException($"Concurrency conflict updating {typeof(TEntity).Name}.", typeof(TEntity).Name, nameof(UpdateAsync), null, ex);
        }
        catch (Exception ex) when (ex is not ConcurrencyException and not DataAccessException)
        {
            throw new DataAccessException(ex.Message, typeof(TEntity).Name, nameof(UpdateAsync), null, ex);
        }
    }
}
