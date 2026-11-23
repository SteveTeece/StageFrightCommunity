using CommunityToolkit.Maui.Storage;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;

namespace StageFright.App;

/// <summary>
/// <see cref="IBackupDestinationPicker"/> backed by CommunityToolkit.Maui's <see cref="FileSaver"/> —
/// the OS-native "Save As" dialog on Windows and Mac Catalyst. A user dismiss returns
/// <see cref="BackupDestinationResult.Cancelled"/>; any platform error is wrapped as a
/// <see cref="DataAccessException"/> before it crosses back into Core.
/// </summary>
public sealed class MauiBackupDestinationPicker : IBackupDestinationPicker
{
    public async Task<BackupDestinationResult> SaveAsync(string suggestedFileName, Stream content, CancellationToken ct = default)
    {
        var result = await FileSaver.Default.SaveAsync(suggestedFileName, content, ct);

        if (result.IsSuccessful)
            return BackupDestinationResult.Saved(result.FilePath);

        if (result.IsCancelled)
            return BackupDestinationResult.Cancelled;

        throw new DataAccessException(
            $"The native save dialog failed: {result.Exception.Message}",
            "Backup",
            nameof(SaveAsync),
            innerException: result.Exception);
    }
}
