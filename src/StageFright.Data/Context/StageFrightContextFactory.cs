using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StageFright.Data.Context;

/// <summary>
/// Factory for creating StageFrightContext instances during EF Core migrations.
/// Used by EF Core CLI tools (dotnet ef) to create a DbContext for migration generation.
/// </summary>
public class StageFrightContextFactory : IDesignTimeDbContextFactory<StageFrightContext>
{
	public StageFrightContext CreateDbContext(string[] args)
	{
		var optionsBuilder = new DbContextOptionsBuilder<StageFrightContext>();

		// Default to SQLite with local database file in TestData folder at repo root
		var repoRoot = FindRepositoryRoot();
		var dbPath = Path.Combine(repoRoot, "TestData", "stagefright.db");
		var connectionString = $"Data Source={dbPath}";

		// Allow command-line override for database path
		if (args.Length > 0)
		{
			connectionString = args[0];
		}

		optionsBuilder.UseSqlite(connectionString);

		return new StageFrightContext(optionsBuilder.Options);
	}

	/// <summary>
	/// Locates the repository root by searching for the solution file or .git directory.
	/// </summary>
	private static string FindRepositoryRoot()
	{
		var currentDirectory = AppContext.BaseDirectory;

		while (currentDirectory != null)
		{
			// Look for .git directory or .sln file to identify repo root
			if (Directory.Exists(Path.Combine(currentDirectory, ".git")) ||
				Directory.GetFiles(currentDirectory, "*.sln").Length > 0)
			{
				return currentDirectory;
			}

			currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
		}

		// Fallback to AppContext.BaseDirectory if repo root not found
		return AppContext.BaseDirectory;
	}
}
