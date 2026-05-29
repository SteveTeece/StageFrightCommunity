namespace StageFright.Core.Services;

/// <summary>
/// Service for file operations in the MAUI/Blazor Hybrid environment.
/// Provides platform-agnostic file saving and retrieval without JavaScript interop.
/// </summary>
public interface IFileService
{
	/// <summary>
	/// Saves file bytes to the device filesystem.
	/// </summary>
	/// <param name="fileName">Name of the file to save (with extension)</param>
	/// <param name="fileBytes">Byte content to save</param>
	/// <param name="folderName">Optional subfolder name (e.g., "Reports", "Exports")</param>
	/// <returns>Full path to the saved file</returns>
	Task<string> SaveFileAsync(string fileName, byte[] fileBytes, string? folderName = null);

	/// <summary>
	/// Gets the path to the export folder for the application.
	/// </summary>
	/// <returns>Full path to export folder</returns>
	Task<string> GetExportFolderAsync();

	/// <summary>
	/// Opens the file in the system's default application.
	/// </summary>
	/// <param name="filePath">Full path to the file to open</param>
	Task<bool> OpenFileAsync(string filePath);
}
