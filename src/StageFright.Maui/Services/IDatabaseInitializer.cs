namespace StageFright.Maui.Services;

/// <summary>
/// Service for initializing the database and running migrations at application startup.
/// </summary>
public interface IDatabaseInitializer
{
	/// <summary>
	/// Initializes the database, creating it if needed and running any pending migrations.
	/// </summary>
	Task InitializeDatabaseAsync();
}
