using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Reports.Rendering;

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

    private List<AnnualGeneralMeeting> _agms = [];
    private bool _loading = true;
    private string? _printMessage;

    protected override async Task OnInitializedAsync()
    {
        _agms = (await AgmService.GetPastAsync()).ToList();
        _loading = false;
    }

    private static int AttendedCount(AnnualGeneralMeeting agm) =>
        agm.AttendanceRecords.Count(a => a.Attended);

    private void OpenDetail(AnnualGeneralMeeting agm) =>
        Nav.NavigateTo($"/events/agm/{agm.Id}");

    private async Task PrintAttendanceReport(Guid agmId)
    {
        _printMessage = null;

        try
        {
            var sheetData = await AgmAttendanceSheetService.GenerateAsync(agmId);

            if (sheetData.Members.Count == 0)
            {
                _printMessage = "No attendance records found — nothing to print.";
                return;
            }

            var settings = await SettingsService.GetAsync();
            var orgName = settings?.OrganizationName ?? string.Empty;
            var bytes = AgmAttendanceSheetPdfRenderer.Render(sheetData, orgName);
            var tempPath = Path.Combine(Path.GetTempPath(), $"agm-attendance-report_{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(tempPath, bytes);
#pragma warning disable CA1416
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath) { UseShellExecute = true });
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to print attendance report for AGM {AgmId}", agmId);
            _printMessage = "Unable to print attendance report. Please try again.";
        }
    }
}
