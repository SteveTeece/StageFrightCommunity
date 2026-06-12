namespace StageFright.Plugins.Contracts;

/// <summary>Metric data returned by an IDashboardTileProvider for rendering.</summary>
public class TileData
{
    /// <summary>Label-to-formatted-value pairs displayed in the tile body (e.g., "Active Members" → "42").</summary>
    public IReadOnlyDictionary<string, string> Metrics { get; init; } = new Dictionary<string, string>();

    /// <summary>Optional CSS colour expression for the tile accent (e.g., "hsl(120,35%,70%)" for green balance).</summary>
    public string? AccentColor { get; init; }

    /// <summary>Optional Blazor route for click-through navigation from the tile.</summary>
    public string? NavigateRoute { get; init; }
}
