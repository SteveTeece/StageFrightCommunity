using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Reports.Rendering;

namespace StageFright.UI.Pages.Events;

/// <summary>
/// Read-only view of a saved AGM: attendance count and every elected position (FR-011,
/// FR-016). Once saved, an AGM has no edit path — corrections are a new superseding AGM.
/// </summary>
public partial class AgmDetail : ComponentBase
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private IAgmService AgmService { get; set; } = null!;
    [Inject] private ICommitteeService CommitteeService { get; set; } = null!;
    [Inject] private IAgmAttendanceRepository AttendanceRepository { get; set; } = null!;
    [Inject] private IAgmAttendanceSheetService AgmAttendanceSheetService { get; set; } = null!;
    [Inject] private IAgmAttendanceSheetPdfRenderer AgmAttendanceSheetPdfRenderer { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private ILogger<AgmDetail> Logger { get; set; } = null!;

    private AnnualGeneralMeeting? _agm;
    private List<AgmAttendanceRecord> _attendance = [];
    private List<CommitteePositionRecord> _positions = [];
    private bool _loading = true;
    private bool _notFound;
    private bool _archiving;
    private string? _printMessage;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _notFound = false;

        _agm = await AgmService.GetByIdAsync(Id);
        if (_agm is null)
        {
            _notFound = true;
            _loading = false;
            return;
        }

        _attendance = (await AttendanceRepository.GetByAgmAsync(Id)).ToList();
        _positions = (await CommitteeService.GetByAgmAsync(Id)).ToList();
        _loading = false;
    }

    private int AttendedCount => _attendance.Count(a => a.Attended);

    /// <summary>
    /// One line per office-holder title (dated multi-holder list per FR-029 when a special
    /// election replaced someone in this term) plus one combined "General Committee Member"
    /// line — general committee is a multi-occupant category, never dated even with several
    /// members, matching CommitteeReportProvider's equivalent split.
    /// </summary>
    private List<(string Label, string MemberText)> PositionLines => BuildPositionLines(_positions);

    private static List<(string Label, string MemberText)> BuildPositionLines(List<CommitteePositionRecord> positions)
    {
        var lines = new List<(string Label, string MemberText)>();

        var officeHolderGroups = positions
            .Where(p => p.OfficeHolderTypeId is not null)
            .GroupBy(p => p.OfficeHolderTypeId!.Value)
            .OrderBy(g => g.First().OfficeHolderType?.DisplayOrder ?? int.MaxValue);

        foreach (var group in officeHolderGroups)
        {
            var holders = group.ToList();
            var label = holders[0].OfficeHolderType?.Name ?? "Unknown Position";
            lines.Add((label, DescribeHolders(holders)));
        }

        var generalMembers = positions.Where(p => p.OfficeHolderTypeId is null).ToList();
        if (generalMembers.Count > 0)
        {
            var names = string.Join(", ", generalMembers
                .Select(m => m.Member.SortableFullName)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            lines.Add(("General Committee Member", names));
        }

        return lines;
    }

    private static string DescribeHolders(List<CommitteePositionRecord> holders)
    {
        if (holders.Count == 1)
            return holders[0].Member.SortableFullName;

        return string.Join(", ", holders
            .OrderBy(h => h.StartDate)
            .Select(h =>
            {
                var start = h.StartDate!.Value.ToString("d MMM yyyy");
                var end = h.EndDate.HasValue ? h.EndDate.Value.ToString("d MMM yyyy") : "present";
                return $"{h.Member.SortableFullName} ({start}–{end})";
            }));
    }

    private async Task ArchiveAsync()
    {
        _archiving = true;
        try
        {
            await AgmService.ArchiveAsync(Id, "coordinator");
            Nav.NavigateTo("/events/agm");
        }
        finally
        {
            _archiving = false;
        }
    }

    private async Task PrintAttendanceReport()
    {
        _printMessage = null;

        try
        {
            var sheetData = await AgmAttendanceSheetService.GenerateAsync(Id);

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
            Logger.LogError(ex, "Failed to print attendance report for AGM {AgmId}", Id);
            _printMessage = "Unable to print attendance report. Please try again.";
        }
    }
}
