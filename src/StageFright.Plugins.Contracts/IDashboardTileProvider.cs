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

    /// <summary>Optional Blazor route to navigate to when the tile card is clicked. Null disables click-through.</summary>
    string? NavigateRoute => null;

    /// <summary>
    /// Optional label for the header action link (e.g. "View Members"). Rendered only when
    /// both this and <see cref="NavigateRoute"/> are non-null. Null hides the link.
    /// </summary>
    string? ActionText => null;

    /// <summary>
    /// Pre-set size the tile should render at on the Dashboard grid. Defaults to 1x1
    /// (OneByOne) for providers that don't override it, matching current behaviour.
    /// </summary>
    DashboardTileSize TileSize => DashboardTileSize.OneByOne;

    /// <summary>Loads tile metric data asynchronously. Exceptions are caught by the dashboard.</summary>
    Task<TileData> GetTileDataAsync(CancellationToken ct);
}
