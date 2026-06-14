namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>
/// Summary information from a backup file, returned by IBackupService.GetManifestAsync.
/// Displayed in the restore confirmation dialog so the user can verify counts before committing.
/// </summary>
public sealed class BackupManifest
{
    public string SchemaVersion { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; }
    public string ApplicationVersion { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, int> EntityCounts { get; init; } = new Dictionary<string, int>();
}
