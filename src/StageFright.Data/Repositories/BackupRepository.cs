using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Settings.Backup;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.Data.Repositories;

/// <summary>
/// Data-access implementation for backup/restore.
/// Reads all entities with IgnoreQueryFilters (includes soft-deleted records) for export.
/// Performs PK-based upserts for import — inside the caller's IUnitOfWork transaction.
/// </summary>
public class BackupRepository : IBackupRepository
{
    private readonly StageFrightDbContext _db;

    public BackupRepository(StageFrightDbContext db)
    {
        _db = db;
    }

    public async Task<BackupSnapshot> GetFullSnapshotAsync(CancellationToken ct = default)
    {
        try
        {
            return new BackupSnapshot
            {
                Members = await _db.Members.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct),
                CommitteeMemberships = await _db.CommitteeMemberships.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct),
                Rehearsals = await _db.Rehearsals.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct),
                AttendanceRecords = await _db.AttendanceRecords.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct),
                Events = await _db.Events.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct),
                EventTypes = await _db.EventTypes.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct),
                ParticipationRecords = await _db.ParticipationRecords.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct),
                Fees = await _db.Fees.AsNoTracking().ToListAsync(ct),
                Payments = await _db.Payments.AsNoTracking().ToListAsync(ct),
                Transactions = await _db.Transactions.AsNoTracking().ToListAsync(ct),
                Accounts = await _db.Accounts.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct),
                Settings = await _db.Settings.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(ct),
                AuditTrailEntries = await _db.AuditTrailEntries.AsNoTracking().ToListAsync(ct)
            };
        }
        catch (Exception ex)
        {
            throw new DataAccessException($"Failed to read backup snapshot: {ex.Message}", "Backup", nameof(GetFullSnapshotAsync), null, ex);
        }
    }

    public async Task UpsertSnapshotAsync(BackupSnapshot snapshot, CancellationToken ct = default)
    {
        try
        {
            // Detach all tracked entities so fresh POCOs from the backup file can be attached
            // without triggering identity-key conflicts with previously loaded instances.
            _db.ChangeTracker.Clear();

            await UpsertCollectionAsync(snapshot.Members, _db.Members, m => m.Id, ct);
            await UpsertCollectionAsync(snapshot.CommitteeMemberships, _db.CommitteeMemberships, c => c.Id, ct);
            await UpsertCollectionAsync(snapshot.Rehearsals, _db.Rehearsals, r => r.Id, ct);
            await UpsertCollectionAsync(snapshot.AttendanceRecords, _db.AttendanceRecords, a => a.Id, ct);
            await UpsertCollectionAsync(snapshot.EventTypes, _db.EventTypes, et => et.Id, ct);
            await UpsertCollectionAsync(snapshot.Events, _db.Events, e => e.Id, ct);
            await UpsertCollectionAsync(snapshot.ParticipationRecords, _db.ParticipationRecords, pr => pr.Id, ct);
            await UpsertCollectionAsync(snapshot.Accounts, _db.Accounts, c => c.Id, ct);
            await UpsertCollectionAsync(snapshot.Fees, _db.Fees, f => f.Id, ct);
            await UpsertCollectionAsync(snapshot.Payments, _db.Payments, p => p.Id, ct);
            await UpsertCollectionAsync(snapshot.Transactions, _db.Transactions, t => t.Id, ct);

            if (snapshot.Settings is not null)
                await UpsertSettingsAsync(snapshot.Settings, ct);

            await UpsertCollectionAsync(snapshot.AuditTrailEntries, _db.AuditTrailEntries, a => a.Id, ct);

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException($"Upsert failed during restore: {ex.Message}", "Backup", nameof(UpsertSnapshotAsync), null, ex);
        }
    }

    private async Task UpsertCollectionAsync<T>(
        IReadOnlyList<T> entities,
        DbSet<T> dbSet,
        Func<T, Guid> getKey,
        CancellationToken ct) where T : class
    {
        foreach (var entity in entities)
        {
            var id = getKey(entity);
            var exists = await dbSet.IgnoreQueryFilters().AnyAsync(e => EF.Property<Guid>(e, "Id") == id, ct);
            if (exists)
                _db.Entry(entity).State = EntityState.Modified;
            else
                dbSet.Add(entity);
        }
    }

    private async Task UpsertSettingsAsync(SettingsEntity settings, CancellationToken ct)
    {
        var exists = await _db.Settings.IgnoreQueryFilters().AnyAsync(s => s.Id == settings.Id, ct);
        if (exists)
            _db.Entry(settings).State = EntityState.Modified;
        else
            _db.Settings.Add(settings);
    }
}
