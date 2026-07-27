using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Rehearsals;
using StageFright.Reports.Rendering;

namespace StageFright.UI.Pages.Rehearsals;

public partial class RehearsalList
{
    [Inject] private IRehearsalService RehearsalService { get; set; } = null!;
    [Inject] private IAttendanceRollService AttendanceRollService { get; set; } = null!;
    [Inject] private IAttendanceRollPdfRenderer AttendanceRollPdfRenderer { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

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
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load rehearsals: {ex.Message}";
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
                _rollMessage = "No active members found — nothing to print.";
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
        catch (Exception)
        {
            _rollMessage = "Unable to print roll. Please try again.";
        }
    }
}
