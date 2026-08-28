using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Rehearsals;
using StageFright.Reports.Rendering;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Rehearsals;

public partial class RehearsalList
{
    [Inject] private IRehearsalService RehearsalService { get; set; } = null!;
    [Inject] private IAttendanceRollService AttendanceRollService { get; set; } = null!;
    [Inject] private IAttendanceRollPdfRenderer AttendanceRollPdfRenderer { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private ILogger<RehearsalList> Logger { get; set; } = null!;
    [Inject] private IStringLocalizer<RehearsalsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private bool _loading = true;
    private List<Rehearsal> _rehearsals = new();
    private string _searchTerm = string.Empty;
    private string? _errorMessage;
    private string? _rollMessage;

    private IEnumerable<Rehearsal> DisplayRehearsals =>
        string.IsNullOrWhiteSpace(_searchTerm)
            ? _rehearsals
            : _rehearsals.Where(r =>
                r.Date.ToString("d MMM yyyy").Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ||
                (r.Notes?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false));

    private const int MaxFutureRehearsals = 3;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var today = DateTime.Today;
            var result = await RehearsalService.GetAllAsync();

            var futureRehearsals = result
                .Where(r => r.Date.Date >= today)
                .OrderBy(r => r.Date)
                .ThenBy(r => r.Time)
                .Take(MaxFutureRehearsals);

            var pastRehearsals = result
                .Where(r => r.Date.Date < today && r.Date.Year == today.Year);

            _rehearsals = futureRehearsals
                .Concat(pastRehearsals)
                .OrderByDescending(r => r.Date)
                .ThenByDescending(r => r.Time)
                .ToList();
        }
        catch (Exception)
        {
            _errorMessage = L["Rehearsals_List_LoadError"];
        }
        finally
        {
            _loading = false;
        }
    }

    private void AddRehearsal() => Nav.NavigateTo("/rehearsals/new");

    private void RecordAttendance(Guid rehearsalId) =>
        Nav.NavigateTo($"/rehearsals/{rehearsalId}/attendance");

    private async Task PrintRoll(Guid rehearsalId)
    {
        _rollMessage = null;

        try
        {
            var rollData = await AttendanceRollService.GenerateAsync(rehearsalId);

            if (rollData.Members.Count == 0)
            {
                _rollMessage = L["Rehearsals_List_RollNoMembers"];
                return;
            }

            var settings = await SettingsService.GetAsync();
            var orgName = settings?.OrganizationName ?? string.Empty;
            var bytes = AttendanceRollPdfRenderer.Render(rollData, orgName);
            var tempPath = Path.Combine(Path.GetTempPath(), $"attendance-roll_{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(tempPath, bytes);
#pragma warning disable CA1416
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath) { UseShellExecute = true });
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to print attendance roll for rehearsal {RehearsalId}", rehearsalId);
            _rollMessage = L["Rehearsals_List_RollError"];
        }
    }

    /// <summary>"No rehearsals match …" message, localized with the current search term.</summary>
    private string NoMatchText() => Loc.Get<RehearsalsResource>("Rehearsals_List_NoMatch", _searchTerm);

    /// <summary>aria-label for the date button that opens a rehearsal's recorded attendance.</summary>
    private string ViewAttendanceAriaLabel(DateTime date) =>
        Loc.Get<RehearsalsResource>("Rehearsals_List_ViewAttendanceAriaLabel", date.ToString("d MMM yyyy"));

    /// <summary>aria-label for a row's Record Attendance button.</summary>
    private string RecordAttendanceAriaLabel(DateTime date) =>
        Loc.Get<RehearsalsResource>("Rehearsals_List_RecordAttendanceAriaLabel", date.ToString("d MMM yyyy"));

    /// <summary>aria-label for a row's Print Roll button.</summary>
    private string PrintRollAriaLabel(DateTime date) =>
        Loc.Get<RehearsalsResource>("Rehearsals_List_PrintRollAriaLabel", date.ToString("d MMM yyyy"));
}
