using StageFright.Data.Repositories;
using StageFright.Plugins.Contracts;

namespace StageFright.Plugins.Providers;

/// <summary>
/// Dashboard tile provider for Rehearsals module.
/// Displays most recent rehearsal date, stored attendance rate, and total count.
/// </summary>
public class RehearsalsDashboardTileProvider : IDashboardTileProvider
{
    private readonly IRehearsalRepository _rehearsalRepository;

    public string TileId => "rehearsals-tile";
    public string DisplayName => "Rehearsals";
    public string ModuleName => "Rehearsals";
    public int DisplayOrder => 2;

    public RehearsalsDashboardTileProvider(IRehearsalRepository rehearsalRepository)
    {
        _rehearsalRepository = rehearsalRepository ?? throw new ArgumentNullException(nameof(rehearsalRepository));
    }

    public async Task<TileData> GenerateAsync()
    {
        try
        {
            var rehearsals = await _rehearsalRepository.GetAllAsync();
            var totalCount = rehearsals.Count();
            var mostRecent = rehearsals.OrderByDescending(r => r.Date).FirstOrDefault();

            var metrics = new Dictionary<string, string>
            {
                { "Total Recorded", totalCount.ToString() }
            };

            if (mostRecent != null)
            {
                metrics.Add("Most Recent", mostRecent.Date.ToShortDateString());
                metrics.Add("Attendance Rate", $"{mostRecent.StoredAttendanceRate:F1}%");
            }

            return new TileData
            {
                Title = "Rehearsals",
                Content = $"Total: {totalCount} | Most Recent: {mostRecent?.Date.ToShortDateString() ?? "None"}",
                Metrics = metrics,
                IsError = false
            };
        }
        catch (Exception ex)
        {
            return new TileData
            {
                Title = "Rehearsals",
                IsError = true,
                ErrorMessage = $"Error loading rehearsals data: {ex.Message}"
            };
        }
    }
}
