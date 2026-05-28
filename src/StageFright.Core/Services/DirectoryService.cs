namespace StageFright.Core.Services;

/// <summary>
/// Directory auto-creation service for plugin discovery and configuration directories.
/// Per FR-021: Auto-creates Plugins directory on startup if it doesn't exist.
/// </summary>
public interface IDirectoryService
{
    /// <summary>Ensure a directory exists, creating it if necessary</summary>
    /// <param name="directoryPath">Full path to the directory</param>
    void EnsureDirectoryExists(string directoryPath);

    /// <summary>Get or create the plugins directory</summary>
    /// <returns>Full path to the plugins directory</returns>
    string GetOrCreatePluginsDirectory();

    /// <summary>Get or create the backup directory</summary>
    /// <returns>Full path to the backup directory</returns>
    string GetOrCreateBackupDirectory();

    /// <summary>Get or create a settings directory</summary>
    /// <returns>Full path to the settings directory</returns>
    string GetOrCreateSettingsDirectory();
}

/// <summary>
/// Default implementation of IDirectoryService.
/// Manages application directory structure and auto-creation.
/// </summary>
public class DirectoryService : IDirectoryService
{
    private readonly string _appDataPath;

    public DirectoryService()
    {
        // Use LocalApplicationData folder for cross-platform compatibility
        _appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StageFright"
        );
    }

    public void EnsureDirectoryExists(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            try
            {
                Directory.CreateDirectory(directoryPath);
                System.Diagnostics.Debug.WriteLine($"Created directory: {directoryPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating directory {directoryPath}: {ex.Message}");
            }
        }
    }

    public string GetOrCreatePluginsDirectory()
    {
        var pluginsPath = Path.Combine(_appDataPath, "Plugins");
        EnsureDirectoryExists(pluginsPath);
        return pluginsPath;
    }

    public string GetOrCreateBackupDirectory()
    {
        var backupPath = Path.Combine(_appDataPath, "Backups");
        EnsureDirectoryExists(backupPath);
        return backupPath;
    }

    public string GetOrCreateSettingsDirectory()
    {
        var settingsPath = Path.Combine(_appDataPath, "Settings");
        EnsureDirectoryExists(settingsPath);
        return settingsPath;
    }
}
