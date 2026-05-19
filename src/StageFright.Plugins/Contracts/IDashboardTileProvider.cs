namespace StageFright.Plugins.Contracts;

using System.Threading.Tasks;

/// <summary>
/// Contract for dashboard tile providers.
/// Plugins implement this to contribute tiles to the dashboard.
/// </summary>
public interface IDashboardTileProvider
{
	/// <summary>Unique identifier for this tile.</summary>
	string TileId { get; }

	/// <summary>Display name for the tile.</summary>
	string DisplayName { get; }

	/// <summary>Module this tile belongs to (e.g., "Members", "Finance").</summary>
	string ModuleName { get; }

	/// <summary>Display order for positioning (lower numbers = left/top).</summary>
	int DisplayOrder { get; }

	/// <summary>Generates the tile content asynchronously.</summary>
	Task<TileData> GenerateAsync();
}

