using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Agm;
using StageFright.Reports.Rendering;
using StageFright.UI.Resources.Strings;

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
    [Inject] private IAgmResultsPdfRenderer AgmResultsPdfRenderer { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private ILogger<AgmDetail> Logger { get; set; } = null!;
    [Inject] private IStringLocalizer<EventsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private AnnualGeneralMeeting? _agm;
    private List<AgmAttendanceRecord> _attendance = [];
    private List<CommitteePositionRecord> _positions = [];
    private bool _loading = true;
    private bool _notFound;
    private bool _archiving;
    private string? _printMessage;

    private string DetailHeadingText() =>
        Loc.Get<EventsResource>("Events_Agm_DetailHeading", _agm!.Date.ToString("d MMMM yyyy"));

    private string AttendanceSummaryText() =>
        Loc.Get<EventsResource>("Events_Agm_AttendanceSummary", AttendedCount, _attendance.Count);

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
    /// One line per office-holder title, dated multi-holder list per FR-029 when a special
    /// election replaced someone in this term. General committee members (no office-holder
    /// title) are handled separately by <see cref="GeneralCommitteeMemberNames"/> — issue #307
    /// moved that group from a comma-joined line here to a one-name-per-row list box.
    /// </summary>
    private List<(string Label, string MemberText)> PositionLines => BuildPositionLines(_positions);

    /// <summary>
    /// General committee members (positions with no named office-holder title), sorted
    /// alphabetically, one name per entry — rendered as individual list-box rows instead of the
    /// single comma-separated line used before issue #307. Empty when none are recorded.
    /// </summary>
    private List<string> GeneralCommitteeMemberNames => _positions
        .Where(p => p.OfficeHolderTypeId is null)
        .Select(m => m.Member.SortableFullName)
        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private List<(string Label, string MemberText)> BuildPositionLines(List<CommitteePositionRecord> positions)
    {
        var lines = new List<(string Label, string MemberText)>();

        var officeHolderGroups = positions
            .Where(p => p.OfficeHolderTypeId is not null)
            .GroupBy(p => p.OfficeHolderTypeId!.Value)
            .OrderBy(g => g.First().OfficeHolderType?.DisplayOrder ?? int.MaxValue);

        foreach (var group in officeHolderGroups)
        {
            var holders = group.ToList();
            var label = holders[0].OfficeHolderType?.Name ?? L["Events_Agm_UnknownPosition"].Value;
            lines.Add((label, DescribeHolders(holders)));
        }

        return lines;
    }

    private string DescribeHolders(List<CommitteePositionRecord> holders)
    {
        if (holders.Count == 1)
            return holders[0].Member.SortableFullName;

        return string.Join(", ", holders
            .OrderBy(h => h.StartDate)
            .Select(h =>
            {
                var start = h.StartDate!.Value.ToString("d MMM yyyy");
                var end = h.EndDate.HasValue ? h.EndDate.Value.ToString("d MMM yyyy") : L["Events_Agm_HolderPresent"].Value;
                return Loc.Get<EventsResource>("Events_Agm_HolderTerm", h.Member.SortableFullName, start, end);
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
                _printMessage = L["Events_Agm_PrintNoRecords"];
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
            _printMessage = L["Events_Agm_PrintError"];
        }
    }

    private async Task PrintAgmResults()
    {
        _printMessage = null;

        try
        {
            var data = new AgmResultsData
            {
                AgmDate = _agm!.Date,
                AttendedCount = AttendedCount,
                TotalCount = _attendance.Count,
                PositionLines = PositionLines
                    .Select(l => new AgmResultsPositionLine { Label = l.Label, MemberText = l.MemberText })
                    .ToList(),
                GeneralCommitteeMemberNames = GeneralCommitteeMemberNames
            };

            var settings = await SettingsService.GetAsync();
            var orgName = settings?.OrganizationName ?? string.Empty;
            var bytes = AgmResultsPdfRenderer.Render(data, orgName);
            var tempPath = Path.Combine(Path.GetTempPath(), $"agm-results-report_{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(tempPath, bytes);
#pragma warning disable CA1416
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath) { UseShellExecute = true });
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to print AGM results report for AGM {AgmId}", Id);
            _printMessage = L["Events_Agm_PrintResultsError"];
        }
    }
}
