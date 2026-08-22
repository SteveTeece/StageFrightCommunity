using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;

namespace StageFright.Data.Repositories;

/// <summary>
/// Extends BaseRepository with soft-delete operations. Uses reflection to access
/// IsDeleted/DeletedAt/DeletedBy fields that all soft-deletable entities share.
/// Validates that an entity is not already deleted before archiving.
/// </summary>
public class SoftDeletableBaseRepository<TEntity> : BaseRepository<TEntity>, ISoftDeletableRepository<TEntity>
    where TEntity : class
{
    public SoftDeletableBaseRepository(StageFrightDbContext db) : base(db) { }

    public virtual async Task ArchiveAsync(Guid id, string deletedBy, CancellationToken ct = default)
    {
        try
        {
            var entity = await _db.Set<TEntity>().FindAsync(new object[] { id }, ct)
                ?? throw new EntityNotFoundException(typeof(TEntity).Name, id, nameof(ArchiveAsync));

            var isDeletedProp = typeof(TEntity).GetProperty("IsDeleted")!;
            if ((bool)isDeletedProp.GetValue(entity)!)
                throw new ValidationException($"{typeof(TEntity).Name} is already archived.", typeof(TEntity).Name, nameof(ArchiveAsync), id);

            isDeletedProp.SetValue(entity, true);
            typeof(TEntity).GetProperty("DeletedAt")!.SetValue(entity, DateTime.UtcNow);
            typeof(TEntity).GetProperty("DeletedBy")!.SetValue(entity, deletedBy);

            var updatedAtProp = typeof(TEntity).GetProperty("UpdatedAt");
            updatedAtProp?.SetValue(entity, DateTime.UtcNow);

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not EntityNotFoundException and not ValidationException and not DataAccessException)
        {
            throw new DataAccessException(ex.Message, typeof(TEntity).Name, nameof(ArchiveAsync), id, ex);
        }
    }

    public virtual async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var entity = await _db.Set<TEntity>().IgnoreQueryFilters().FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id, ct)
                ?? throw new EntityNotFoundException(typeof(TEntity).Name, id, nameof(RestoreAsync));

            typeof(TEntity).GetProperty("IsDeleted")!.SetValue(entity, false);
            typeof(TEntity).GetProperty("DeletedAt")!.SetValue(entity, null);
            typeof(TEntity).GetProperty("DeletedBy")!.SetValue(entity, null);

            var updatedAtProp = typeof(TEntity).GetProperty("UpdatedAt");
            updatedAtProp?.SetValue(entity, DateTime.UtcNow);

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not EntityNotFoundException and not DataAccessException)
        {
            throw new DataAccessException(ex.Message, typeof(TEntity).Name, nameof(RestoreAsync), id, ex);
        }
    }

    public virtual async Task<IReadOnlyList<TEntity>> GetArchivedAsync(CancellationToken ct = default)
    {
        return await _db.Set<TEntity>()
            .IgnoreQueryFilters()
            .Where(e => EF.Property<bool>(e, "IsDeleted"))
            .ToListAsync(ct);
    }
}
