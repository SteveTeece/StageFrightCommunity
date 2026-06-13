using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Dashboard;

/// <summary>
/// Result of loading a single dashboard tile. Carries either the loaded TileData
/// or the exception that was caught — errors never propagate to sibling tiles.
/// </summary>
public sealed class TileLoadResult
{
    /// <summary>The provider that was loaded.</summary>
    public IDashboardTileProvider Provider { get; }

    /// <summary>Tile data when load succeeded; null on error.</summary>
    public TileData? Data { get; }

    /// <summary>Exception caught during load; null on success.</summary>
    public Exception? Error { get; }

    /// <summary>True when Data is available and Error is null.</summary>
    public bool IsSuccess => Error is null;

    public TileLoadResult(IDashboardTileProvider provider, TileData? data, Exception? error)
    {
        Provider = provider;
        Data = data;
        Error = error;
    }
}
