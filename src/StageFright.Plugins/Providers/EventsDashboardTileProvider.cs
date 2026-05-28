using StageFright.Data.Repositories;
using StageFright.Plugins.Contracts;

namespace StageFright.Plugins.Providers;

/// <summary>
/// Dashboard tile provider for Events module.
/// Displays most recent event date, stored participation rate, and total count.
/// </summary>
public class EventsDashboardTileProvider : IDashboardTileProvider
{
    private readonly IEventRepository _eventRepository;

    public string TileId => "events-tile";
    public string DisplayName => "Events";
    public string ModuleName => "Events";
    public int DisplayOrder => 3;

    public EventsDashboardTileProvider(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
    }

    public async Task<TileData> GenerateAsync()
    {
        try
        {
            var events = await _eventRepository.GetAllAsync();
            var totalCount = events.Count();
            var mostRecent = events.OrderByDescending(e => e.Date).FirstOrDefault();

            var metrics = new Dictionary<string, string>
            {
                { "Total Recorded", totalCount.ToString() }
            };

            if (mostRecent != null)
            {
                metrics.Add("Most Recent", mostRecent.Date.ToShortDateString());
                metrics.Add("Participation Rate", $"{mostRecent.StoredParticipationRate:F1}%");
            }

            return new TileData
            {
                Title = "Events",
                Content = $"Total: {totalCount} | Most Recent: {mostRecent?.Date.ToShortDateString() ?? "None"}",
                Metrics = metrics,
                IsError = false
            };
        }
        catch (Exception ex)
        {
            return new TileData
            {
                Title = "Events",
                IsError = true,
                ErrorMessage = $"Error loading events data: {ex.Message}"
            };
        }
    }
}
