using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;

namespace StageFright.UI.Modules.Rehearsals;

/// <summary>
/// Dashboard tile body for the Rehearsals module (design 3a): upcoming count, next
/// rehearsal date, and the most recent recorded attendance as "n of m (x%)" with a
/// progress bar (green at ≥80%, accent below).
/// </summary>
public partial class RehearsalsTile : ComponentBase
{
    [Inject] private IRehearsalService RehearsalService { get; set; } = null!;
    [Inject] private IAttendanceRepository AttendanceRepository { get; set; } = null!;

    private bool _loading = true;
    private bool _error;
    private int _upcomingCount;
    private DateTime? _nextDate;
    private int _attendedCount;
    private int _recordCount;
    private decimal? _lastRate;

    private bool HasLastAttendance => _lastRate.HasValue;
    private bool IsGoodRate => _lastRate >= 80m;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var today = DateTime.Today;
            var all = await RehearsalService.GetAllAsync();

            var upcoming = all.Where(r => r.Date.Date >= today).OrderBy(r => r.Date).ToList();
            _upcomingCount = upcoming.Count;
            _nextDate = upcoming.FirstOrDefault()?.Date;

            var lastRecorded = await RehearsalService.GetMostRecentPastWithAttendanceAsync(today);
            if (lastRecorded?.StoredAttendanceRate is { } rate)
            {
                _lastRate = rate;
                var records = await AttendanceRepository.GetByRehearsalAsync(lastRecorded.Id);
                _attendedCount = records.Count(r => r.Attended);
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
