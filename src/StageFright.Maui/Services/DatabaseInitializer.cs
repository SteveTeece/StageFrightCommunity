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
	/// This method is called at application startup and blocks until complete.
	/// </summary>
	public async Task InitializeDatabaseAsync()
	{
		try
		{
			_logger.LogInformation("[STARTUP-DB] Starting database initialization...");

			// Ensure the database file directory exists
			var dbPath = GetDatabasePath();
			_logger.LogInformation("[STARTUP-DB] Database path: {DbPath}", dbPath);
			
			var dbDir = Path.GetDirectoryName(dbPath);
			if (dbDir != null && !Directory.Exists(dbDir))
			{
				_logger.LogInformation("[STARTUP-DB] Creating database directory: {DbDir}", dbDir);
				Directory.CreateDirectory(dbDir);
				_logger.LogInformation("[STARTUP-DB] Database directory created successfully");
			}
			else if (dbDir != null)
			{
				_logger.LogInformation("[STARTUP-DB] Database directory already exists: {DbDir}", dbDir);
			}

			// Run migrations
			_logger.LogInformation("[STARTUP-DB] Starting database migrations...");
			await _context.Database.MigrateAsync();
			_logger.LogInformation("[STARTUP-DB] Database migrations completed successfully.");

			// Ensure database structure is created if migrations didn't
			_logger.LogInformation("[STARTUP-DB] Verifying database connectivity...");
			if (!await _context.Database.CanConnectAsync())
			{
				_logger.LogInformation("[STARTUP-DB] Database connectivity check failed, creating database structure...");
				await _context.Database.EnsureCreatedAsync();
				_logger.LogInformation("[STARTUP-DB] Database structure created.");
			}
			else
			{
				_logger.LogInformation("[STARTUP-DB] Database connectivity verified successfully");
			}

			// Seed test data if database is empty
			_logger.LogInformation("[STARTUP-DB] Checking if database needs seeding...");
			await _seeder.SeedDatabaseAsync(_context);
			_logger.LogInformation("[STARTUP-DB] Database seeding completed.");

			_logger.LogInformation("[STARTUP-DB] Database initialization completed successfully.");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "[STARTUP-DB] Error during database initialization");
			throw;
		}
	}

	/// <summary>
	/// Gets the database file path from the connection string.
	/// </summary>
	private string GetDatabasePath()
	{
		var connectionString = _context.Database.GetConnectionString();
		if (string.IsNullOrEmpty(connectionString))
		{
			throw new InvalidOperationException("Database connection string is not configured.");
		}

		// Extract path from SQLite connection string: "Data Source={path}"
		var dataSourcePrefix = "Data Source=";
		var index = connectionString.IndexOf(dataSourcePrefix, StringComparison.OrdinalIgnoreCase);
		if (index >= 0)
		{
			var path = connectionString.Substring(index + dataSourcePrefix.Length);
			// Remove any trailing parameters
			var semicolonIndex = path.IndexOf(';');
			if (semicolonIndex >= 0)
			{
				path = path.Substring(0, semicolonIndex);
			}
			return path.Trim('"', '\'');
		}

		throw new InvalidOperationException("Could not extract database path from connection string.");
	}
}
