using StageFright.Core.Contracts;

namespace StageFright.App;

/// <summary>
/// Returns <c>FileSystem.AppDataDirectory/recovery</c> as the location for pre-restore recovery
/// copies (FR-015), creating the directory on demand. Never throws — a directory-creation failure
/// is swallowed here and surfaces later as a <see cref="Core.Exceptions.DataAccessException"/> from
/// the export write, matching the never-throw shape of <see cref="MauiLanguagePreferenceStore"/>.
/// </summary>
public sealed class MauiRecoveryCopyStore : IRecoveryCopyStore
{
    public string GetRecoveryDirectory()
    {
        var dir = Path.Combine(FileSystem.AppDataDirectory, "recovery");

        try
        {
            Directory.CreateDirectory(dir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The app-data subdirectory could not be created now; the export write will surface
            // any resulting failure as a DataAccessException. Never throw from the store itself.
        }

        return dir;
    }
}
