namespace StageFright.Core.Contracts;

/// <summary>
/// Native "Save As" seam for choosing where a backup file is written and under what name (FR-009).
/// Implemented per platform (MAUI). Never throws for a user cancel — it returns
/// <see cref="BackupDestinationResult.Cancelled"/>. Any platform error is wrapped as
/// <see cref="Core.Exceptions.DataAccessException"/> by the implementation before it crosses back
/// into Core.
/// </summary>
public interface IBackupDestinationPicker
{
    /// <summary>
    /// Shows the OS-native Save dialog pre-filled with <paramref name="suggestedFileName"/>
    /// (e.g. <c>"Cool Choir backup 2026-09-07.sfbak"</c>), writes <paramref name="content"/> to the
    /// chosen location, and returns the final absolute path — or
    /// <see cref="BackupDestinationResult.Cancelled"/> if the user dismisses the dialog.
    /// </summary>
    Task<BackupDestinationResult> SaveAsync(string suggestedFileName, Stream content, CancellationToken ct = default);
}
