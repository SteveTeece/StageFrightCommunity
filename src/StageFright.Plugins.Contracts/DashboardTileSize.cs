namespace StageFright.Plugins.Contracts;

/// <summary>
/// Pre-set size a dashboard tile can render at. <see cref="OneByOne"/> is the default,
/// matching every tile's current rendered size.
/// </summary>
public enum DashboardTileSize
{
    /// <summary>1 column x 1 row (default).</summary>
    OneByOne,

    /// <summary>2 columns x 1 row (double width).</summary>
    OneByTwo,

    /// <summary>1 column x 2 rows (double height).</summary>
    TwoByOne,

    /// <summary>2 columns x 2 rows (double width and height).</summary>
    TwoByTwo
}
