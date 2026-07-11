using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;

namespace StageFright.UI.Modules.Events;

/// <summary>
/// Dashboard tile body for the Events module (design 3a): upcoming count, next event
/// date, and the most recent recorded participation as "n of m (x%)" with a progress
/// bar (green at ≥80%, accent below).
/// </summary>
public partial class EventsTile : ComponentBase
{
    [Inject] private IEventService EventService { get; set; } = null!;

    private bool _loading = true;
    private bool _error;
    private int _upcomingCount;
    private DateTime? _nextDate;
    private int _participatedCount;
    private int _recordCount;
    private decimal? _lastRate;

    private bool HasLastParticipation => _lastRate.HasValue;
    private bool IsGoodRate => _lastRate >= 80m;

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
