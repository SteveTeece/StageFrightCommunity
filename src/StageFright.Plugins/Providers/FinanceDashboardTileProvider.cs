using StageFright.Plugins.Contracts;

namespace StageFright.Plugins.Providers;

/// <summary>
/// Dashboard tile provider for Finance module placeholder.
/// Displays total outstanding balance (to be fully implemented in Phase 2).
/// </summary>
public class FinanceDashboardTileProvider : IDashboardTileProvider
{
    public string TileId => "finance-tile";
    public string DisplayName => "Finance";
    public string ModuleName => "Finance";
    public int DisplayOrder => 4;

    public async Task<TileData> GenerateAsync()
    {
        try
        {
            // TODO: Implement outstanding balance calculation in Phase 2
            // For now, placeholder shows 0 balance
            var outstandingBalance = 0m;

            return new TileData
            {
                Title = "Finance",
                Content = $"Outstanding Balance: {outstandingBalance:C}",
                Metrics = new Dictionary<string, string>
                {
                    { "Outstanding Balance", outstandingBalance.ToString("C") }
                },
                Color = outstandingBalance >= 0 ? "green" : "red",
                IsError = false
            };
        }
        catch (Exception ex)
        {
            return new TileData
            {
                Title = "Finance",
                IsError = true,
                ErrorMessage = $"Error loading finance data: {ex.Message}"
            };
        }
    }
}
