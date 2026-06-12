namespace StageFright.Plugins.Contracts;

/// <summary>
/// Implemented by both core modules and external plugins to supply a tile on the dashboard.
/// Core tiles use DisplayOrder 0–99; plugin tiles use 100+.
/// Tiles load in parallel; a throwing provider shows "Unable to load" without blocking others.
/// </summary>
public interface IDashboardTileProvider
{
    /// <summary>Unique tile identifier. Duplicate TileIds are skipped with a warning log.</summary>
    string TileId { get; }

    /// <summary>Human-readable title displayed in the tile header.</summary>
    string Title { get; }

    /// <summary>Module name used for grouping and display (e.g., "Members", "Finance").</summary>
    string ModuleName { get; }

    /// <summary>Sort order. Core tiles: 0–99. Plugin tiles: 100+.</summary>
    int DisplayOrder { get; }

    /// <summary>Blazor component type rendered in the tile body.</summary>
    Type TileComponentType { get; }

    /// <summary>Loads tile metric data asynchronously. Exceptions are caught by the dashboard.</summary>
    Task<TileData> GetTileDataAsync(CancellationToken ct);
}
