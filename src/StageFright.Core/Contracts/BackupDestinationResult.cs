namespace StageFright.Core.Contracts;

/// <summary>
/// Outcome of <see cref="IBackupDestinationPicker.SaveAsync"/>: the user either chose a location
/// (<see cref="Saved"/>) or dismissed the Save dialog (<see cref="Cancelled"/>).
/// </summary>
/// <param name="WasCancelled">True when the user dismissed the Save dialog without choosing a location.</param>
/// <param name="FilePath">The absolute path the backup was written to, or <see langword="null"/> when cancelled.</param>
public sealed record BackupDestinationResult(bool WasCancelled, string? FilePath)
{
    /// <summary>The user dismissed the Save dialog; nothing was written.</summary>
    public static BackupDestinationResult Cancelled { get; } = new(true, null);

    /// <summary>The backup was written to <paramref name="path"/>.</summary>
    public static BackupDestinationResult Saved(string path) => new(false, path);
}
