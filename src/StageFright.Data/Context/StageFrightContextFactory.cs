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

		// Default to SQLite with local database file in TestData folder
		var connectionString = "Data Source=TestData/stagefright.db";

		// Allow command-line override for database path
		if (args.Length > 0)
		{
			connectionString = args[0];
		}

		optionsBuilder.UseSqlite(connectionString);

		return new StageFrightContext(optionsBuilder.Options);
	}
}
