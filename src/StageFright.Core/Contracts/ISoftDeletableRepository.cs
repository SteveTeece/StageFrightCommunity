namespace StageFright.Core.Contracts;

/// <summary>
/// Extends IRepository with soft-delete (archive/restore) operations.
/// GetAllAsync from the base contract returns only non-deleted entities.
/// GetArchivedAsync explicitly bypasses the global query filter.
/// Updates to soft-deleted entities are rejected with ValidationException unless restoring.
/// </summary>
public interface ISoftDeletableRepository<TEntity> : IRepository<TEntity> where TEntity : class
{
    /// <summary>Sets IsDeleted=true, DeletedAt=UtcNow, DeletedBy on the entity.</summary>
    Task ArchiveAsync(Guid id, string deletedBy, CancellationToken ct = default);

    /// <summary>Clears IsDeleted, DeletedAt, DeletedBy to restore the entity.</summary>
    Task RestoreAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns entities where IsDeleted=true (bypasses global query filter).</summary>
    Task<IReadOnlyList<TEntity>> GetArchivedAsync(CancellationToken ct = default);
}
