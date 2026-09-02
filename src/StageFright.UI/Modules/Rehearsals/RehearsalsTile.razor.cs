using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Localization;
using StageFright.UI.Resources.Strings;

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
    [Inject] private IStringLocalizer<RehearsalsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private bool _loading = true;
    private bool _error;
    private int _upcomingCount;
    private DateTime? _nextDate;
    private int _attendedCount;
    private int _recordCount;
    private decimal? _lastRate;

    private bool HasLastAttendance => _lastRate.HasValue;
    private bool IsGoodRate => _lastRate >= 80m;

    /// <summary>"n of m (x%)" recorded-attendance detail for the tile note.</summary>
    private string AttendanceDetailText() =>
        Loc.Get<RehearsalsResource>("Rehearsals_Tile_AttendanceDetail",
            _attendedCount, _recordCount, $"{_lastRate:F0}");

    /// <summary>"x%" recorded-attendance rate for the tile note when the record count is unknown.</summary>
    private string AttendanceRateText() =>
        Loc.Get<RehearsalsResource>("Rehearsals_Tile_AttendanceRate", $"{_lastRate:F0}");

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
