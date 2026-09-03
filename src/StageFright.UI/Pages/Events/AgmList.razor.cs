using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Localization;
using StageFright.Reports.Rendering;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Events;

/// <summary>
/// Browsable list of past AGMs, most-recent-first, with date and attendance count (FR-015).
/// Row click opens the read-only AGM detail view.
/// </summary>
public partial class AgmList : ComponentBase
{
    [Inject] private IAgmService AgmService { get; set; } = null!;
    [Inject] private IAgmAttendanceSheetService AgmAttendanceSheetService { get; set; } = null!;
    [Inject] private IAgmAttendanceSheetPdfRenderer AgmAttendanceSheetPdfRenderer { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private ILogger<AgmList> Logger { get; set; } = null!;
    [Inject] private IStringLocalizer<EventsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private List<AnnualGeneralMeeting> _agms = [];
    private bool _loading = true;
    private string? _printMessage;

    protected override async Task OnInitializedAsync()
    {
        _agms = (await AgmService.GetAllAsync()).ToList();
        _loading = false;
    }

    private static int AttendedCount(AnnualGeneralMeeting agm) =>
        agm.AttendanceRecords.Count(a => a.Attended);

    /// <summary>"n of m" attendance summary for an AGM grid row.</summary>
    private string AttendanceValue(AnnualGeneralMeeting agm) =>
        agm.IsRecorded
            ? Loc.Get<EventsResource>("Events_Agm_AttendanceValue", AttendedCount(agm), agm.AttendanceRecords.Count)
            : "—";

    /// <summary>aria-label for a row's Print button.</summary>
    private string PrintReportAriaLabel(DateTime date) =>
        Loc.Get<EventsResource>("Events_Agm_PrintReportAriaLabel", date.ToString("d MMMM yyyy"));

    private void OpenDetail(AnnualGeneralMeeting agm) =>
        Nav.NavigateTo($"/events/agm/{agm.Id}");

    private async Task PrintAttendanceReport(Guid agmId) =>
        _printMessage = await AgmAttendanceReportPrinter.PrintAsync(
            agmId, AgmAttendanceSheetService, AgmAttendanceSheetPdfRenderer, SettingsService, Logger, L);
}
