namespace StageFright.Plugins.Contracts;

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

/// <summary>Data structure for dashboard tile content.</summary>
public class TileData
{
	public string Title { get; set; } = string.Empty;
	public string Content { get; set; } = string.Empty;
	public Dictionary<string, string> Metrics { get; set; } = new();
	public string? Color { get; set; }
	public bool IsError { get; set; }
	public string? ErrorMessage { get; set; }
}
