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
    /// Reads every backed-up entity type from the database (including GL journal entries, bank
    /// reconciliations and their lines, organisations, and spec 033's received items, bank
    /// deposits and deposit lines), including soft-deleted records.
    /// Returns a complete point-in-time snapshot for use in backup export.
    /// </summary>
    Task<BackupSnapshot> GetFullSnapshotAsync(CancellationToken ct = default);

    /// <summary>
    /// Upserts all entities in the snapshot atomically (called inside IUnitOfWork).
    /// For each record: if PK exists → UPDATE non-key fields; if absent → INSERT.
    /// Local records not present in the snapshot are left unchanged.
    /// </summary>
    Task UpsertSnapshotAsync(BackupSnapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// Renames the system cash account (1100) to "Petty Cash" after restoring a file older than
    /// schema 1.4.0, which still calls it "Cash on Hand" (spec 033). As in the AddBankingRework
    /// migration, a non-system account already named "Petty Cash" (any case) becomes
    /// "Petty Cash (own)" first. Called inside the restore's IUnitOfWork transaction.
    /// </summary>
    Task RenameCashAccountToPettyCashAsync(CancellationToken ct = default);
}
