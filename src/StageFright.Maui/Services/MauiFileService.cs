using StageFright.Core.Services;

namespace StageFright.Maui.Services;

/// <summary>
/// MAUI implementation of IFileService for managing file operations.
/// Saves exported files to the device's app cache or documents folder.
/// </summary>
public class MauiFileService : IFileService
{
	/// <summary>
	/// Saves file bytes to the device filesystem in the app's local data folder.
	/// </summary>
	/// <param name="fileName">Name of the file to save (with extension)</param>
	/// <param name="fileBytes">Byte content to save</param>
	/// <param name="folderName">Optional subfolder name (e.g., "Reports", "Exports")</param>
	/// <returns>Full path to the saved file</returns>
	public async Task<string> SaveFileAsync(string fileName, byte[] fileBytes, string? folderName = null)
	{
		try
		{
			// Get the app's local data folder
			var appDataFolder = FileSystem.AppDataDirectory;

			// Create subfolder if specified
			var targetFolder = appDataFolder;
			if (!string.IsNullOrEmpty(folderName))
			{
				targetFolder = Path.Combine(appDataFolder, folderName);
				if (!Directory.Exists(targetFolder))
				{
					Directory.CreateDirectory(targetFolder);
				}
			}

			// Build full file path
			var filePath = Path.Combine(targetFolder, fileName);

			// Write file to disk
			await File.WriteAllBytesAsync(filePath, fileBytes);

			return filePath;
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Failed to save file '{fileName}'", ex);
		}
	}

	/// <summary>
	/// Gets the path to the export folder for the application.
	/// </summary>
	/// <returns>Full path to export folder</returns>
	public async Task<string> GetExportFolderAsync()
	{
		try
		{
			var appDataFolder = FileSystem.AppDataDirectory;
			var exportFolder = Path.Combine(appDataFolder, "Exports");

			if (!Directory.Exists(exportFolder))
			{
				Directory.CreateDirectory(exportFolder);
			}

			return exportFolder;
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException("Failed to get export folder", ex);
		}
	}

	/// <summary>
	/// Opens the file in the system's default application.
	/// </summary>
	/// <param name="filePath">Full path to the file to open</param>
	public async Task<bool> OpenFileAsync(string filePath)
	{
		try
		{
			if (!File.Exists(filePath))
				return false;

			await Launcher.OpenAsync(new OpenFileRequest
			{
				File = new ReadOnlyFile(filePath)
			});

			return true;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error opening file: {ex}");
			return false;
		}
	}
}
