namespace StageFright.Plugins.Contracts;

/// <summary>
/// Contract for custom data access providers.
/// Plugins implement this to extend the data model with custom entities.
/// </summary>
public interface IDataAccessProvider
{
	/// <summary>Unique identifier for this data access provider.</summary>
	string ProviderId { get; }

	/// <summary>Gets the DbContext type for this provider's entities.</summary>
	Type GetDbContextType();

	/// <summary>Registers migrations for this provider's entities.</summary>
	void RegisterMigrations();
}
