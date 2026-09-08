using StageFright.Core.Modules.Settings.Backup;

namespace StageFright.Core.Contracts;

/// <summary>
/// Backup and restore contract. <see cref="CreateBackupAsync"/> is the user-facing entry point —
/// it serialises a full snapshot, hands it to the native "Save As" dialog, reads the written file
/// back from disk and verifies its record counts before reporting success. The lower-level
/// <see cref="ExportAsync"/> writes to an explicit path (used internally for the pre-restore
/// recovery copy). Import validates, retains a pre-restore recovery copy of the current database,
/// then atomically upserts every record from the backup file.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Creates a backup: serialises a full snapshot (soft-deleted records included), shows the
    /// OS-native Save dialog pre-filled with the FR-010 default name
    /// (<c>"&lt;organisation&gt; backup &lt;yyyy-MM-dd&gt;.sfbak"</c>), writes the file, then reads it
    /// back from disk and verifies every per-record-type count against the just-captured live data
    /// and the file's own recorded counts (FR-021–FR-024). Writes an
    /// <see cref="Core.Enums.AuditAction.Export"/> audit entry on success (FR-017).
    /// <para>
    /// Returns a <see cref="BackupVerificationResult"/> with <c>Passed = true</c> only after the
    /// read-back and every count check pass. Throws
    /// <see cref="Core.Exceptions.BackupVerificationException"/> (the file is left on disk for
    /// diagnosis) if the read-back or any count check fails;
    /// <see cref="OperationCanceledException"/> if the user dismisses the Save dialog (no file, no
    /// audit); <see cref="Core.Exceptions.DataAccessException"/> on a write/IO failure (no partial
    /// file left behind).
    /// </para>
    /// </summary>
    Task<BackupVerificationResult> CreateBackupAsync(CancellationToken ct = default);

    /// <summary>
    /// Exports all data to a protobuf binary file at the specified path.
    /// Reads every entity type including soft-deleted records (IgnoreQueryFilters).
    /// After writing, the file is read back from disk and its recorded counts are checked for
    /// internal consistency, so a recovery copy is never silently bad — throws
    /// <see cref="Core.Exceptions.BackupVerificationException"/> if that read-back fails.
    /// </summary>
    Task ExportAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Imports a backup file. Steps: deserialize → full semantic-version check (a file newer than
    /// this build on any version component is rejected) → completeness check → pre-restore recovery
    /// copy (auto-export of the current DB to a durable app-data location, written on every restore)
    /// → atomic PK-upsert → audit.
    /// Throws <see cref="Core.Exceptions.ImportException"/> on any validation failure.
    /// </summary>
    Task ImportAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Reads and validates a backup file without modifying the database.
    /// Returns a <see cref="BackupManifest"/> with entity counts for display in the confirmation dialog.
    /// Throws <see cref="Core.Exceptions.ImportException"/> on corrupt or incompatible files.
    /// </summary>
    Task<BackupManifest> GetManifestAsync(string filePath, CancellationToken ct = default);
}
