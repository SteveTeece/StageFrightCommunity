using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Localization;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Modules.Events;

/// <summary>
/// Dashboard tile body for the Events module (design 3a): upcoming count, next event
/// date, and the most recent recorded participation as "n of m (x%)" with a progress
/// bar (green at ≥80%, accent below).
/// </summary>
public partial class EventsTile : ComponentBase
{
    [Inject] private IEventService EventService { get; set; } = null!;
    [Inject] private IStringLocalizer<EventsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private bool _loading = true;
    private bool _error;
    private int _upcomingCount;
    private DateTime? _nextDate;
    private int _participatedCount;
    private int _recordCount;
    private decimal? _lastRate;

    private bool HasLastParticipation => _lastRate.HasValue;
    private bool IsGoodRate => _lastRate >= 80m;

    /// <summary>"n of m (x%)" recorded-participation detail for the tile note.</summary>
    private string ParticipationDetailText() =>
        Loc.Get<EventsResource>("Events_Tile_ParticipationDetail",
            _participatedCount, _recordCount, $"{_lastRate:F0}");

    /// <summary>"x%" recorded-participation rate for the tile note when the record count is unknown.</summary>
    private string ParticipationRateText() =>
        Loc.Get<EventsResource>("Events_Tile_ParticipationRate", $"{_lastRate:F0}");

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var today = DateTime.Today;
            var all = await EventService.GetAllAsync();

            var upcoming = all.Where(e => e.Date.Date >= today).OrderBy(e => e.Date).ToList();
            _upcomingCount = upcoming.Count;
            _nextDate = upcoming.FirstOrDefault()?.Date;

            var lastRecorded = await EventService.GetMostRecentPastWithParticipationAsync(today);
            if (lastRecorded?.StoredParticipationRate is { } rate)
            {
                _lastRate = rate;
                var details = await EventService.GetByIdWithDetailsAsync(lastRecorded.Id);
                var records = details?.ParticipationRecords ?? [];
                _participatedCount = records.Count(p => p.Participated);
                _recordCount = records.Count;
            }
        }
        catch
        {
            _error = true;
        }
        finally
        {
            _loading = false;
        }
    }
}
