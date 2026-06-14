using StageFright.Core.Modules.Settings.Backup;

namespace StageFright.Core.Contracts;

/// <summary>
/// Backup and restore contract. Export writes all entity data (including soft-deleted records)
/// to a protobuf binary file. Import validates, checkpoints the current database, then
/// atomically upserts every record from the backup file.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Exports all data to a protobuf binary file at the specified path.
    /// Reads every entity type including soft-deleted records (IgnoreQueryFilters).
    /// </summary>
    Task ExportAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Imports a backup file. Steps: deserialize → major-version check → completeness check
    /// → pre-import checkpoint (auto-export of current DB) → atomic PK-upsert → audit.
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
