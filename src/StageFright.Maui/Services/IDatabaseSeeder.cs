using StageFright.Data.Context;

namespace StageFright.Maui.Services;

/// <summary>
/// Service for seeding the database with test data on initial creation.
/// </summary>
public interface IDatabaseSeeder
{
	/// <summary>
	/// Seeds the database with test data if it's empty.
	/// </summary>
	Task SeedDatabaseAsync(StageFrightContext context);
}
