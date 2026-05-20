using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StageFright.Data.Context;

namespace StageFright.Maui.Services;

/// <summary>
/// Implementation of IDatabaseInitializer that handles database initialization and migrations.
/// </summary>
public class DatabaseInitializer : IDatabaseInitializer
{
	private readonly StageFrightContext _context;
	private readonly ILogger<DatabaseInitializer> _logger;
	private readonly IDatabaseSeeder _seeder;

	public DatabaseInitializer(StageFrightContext context, ILogger<DatabaseInitializer> logger, IDatabaseSeeder seeder)
	{
		_context = context;
		_logger = logger;
		_seeder = seeder;
	}

	/// <summary>
	/// Initializes the database, creating it if needed and running any pending migrations.
	/// </summary>
	public async Task InitializeDatabaseAsync()
	{
		try
		{
			_logger.LogInformation("Starting database initialization...");

			// Ensure the database is created and all migrations are applied
			await _context.Database.MigrateAsync();

			_logger.LogInformation("Database migrations completed.");

			// Seed test data if database is empty
			_logger.LogInformation("Seeding database with test data...");
			await _seeder.SeedDatabaseAsync(_context);

			_logger.LogInformation("Database initialized successfully.");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error during database initialization");
			throw;
		}
	}
}
