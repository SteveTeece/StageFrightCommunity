using StageFright.Core.Contracts;
using StageFright.Plugins.Contracts;
using StageFright.UI.Shared;

namespace StageFright.UI.Modules.Dashboard;

/// <summary>
/// Dashboard tile for the Finance module.
/// Displays total outstanding balance. AccentColor reflects balance polarity:
///   positive → HSL(120,35%,70%) green; negative → HSL(0,35%,70%) red; zero → HSL(0,0%,60%) neutral.
/// DisplayOrder=40 places it fourth among core tiles.
/// Placed in StageFright.UI because TileComponentType references a Blazor component.
/// </summary>
public class FinanceDashboardTileProvider : IDashboardTileProvider
{
    private readonly IGLRepository _glRepository;

    public FinanceDashboardTileProvider(IGLRepository glRepository)
    {
        _glRepository = glRepository;
    }

    public string TileId => "finance";
    public string Title => "Finance";
    public string ModuleName => "Finance";
    public int DisplayOrder => 40;
    public Type TileComponentType => typeof(FinanceTileContent);

    public async Task<TileData> GetTileDataAsync(CancellationToken ct)
    {
        var outstanding = await _glRepository.GetTotalOutstandingAsync(ct);

        var accentColor = outstanding switch
        {
            > 0m => "hsl(120, 35%, 70%)",
            < 0m => "hsl(0, 35%, 70%)",
            _ => "hsl(0, 0%, 60%)"
        };

        return new TileData
        {
            Metrics = new Dictionary<string, string>
            {
                { "Outstanding Balance", outstanding.ToString("C") }
            },
            AccentColor = accentColor,
            NavigateRoute = "/finance"
        };
    }
}
