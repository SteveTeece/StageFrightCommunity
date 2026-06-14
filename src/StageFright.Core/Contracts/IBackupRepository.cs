using StageFright.Core.Modules.Settings.Backup;

namespace StageFright.Core.Contracts;

/// <summary>
/// Data-access contract for backup/restore operations.
/// Implementations read all entity data ignoring soft-delete filters (for export),
/// and perform PK-based upserts (for import).
/// </summary>
public interface IBackupRepository
{
    /// <summary>
    /// Reads all 13 entity types from the database, including soft-deleted records.
    /// Returns a complete point-in-time snapshot for use in backup export.
    /// </summary>
    Task<BackupSnapshot> GetFullSnapshotAsync(CancellationToken ct = default);

    /// <summary>
    /// Upserts all entities in the snapshot atomically (called inside IUnitOfWork).
    /// For each record: if PK exists → UPDATE non-key fields; if absent → INSERT.
    /// Local records not present in the snapshot are left unchanged.
    /// </summary>
    Task UpsertSnapshotAsync(BackupSnapshot snapshot, CancellationToken ct = default);
}
