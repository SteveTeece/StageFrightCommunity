using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Events;
using StageFright.Reports.Rendering;

namespace StageFright.UI.Pages.Events;

public partial class EventList
{
    [Inject] private ICombinedEventListService CombinedEventListService { get; set; } = null!;
    [Inject] private IEventAttendanceSheetService EventAttendanceSheetService { get; set; } = null!;
    [Inject] private IEventAttendanceSheetPdfRenderer EventAttendanceSheetPdfRenderer { get; set; } = null!;
    [Inject] private IAgmAttendanceSheetService AgmAttendanceSheetService { get; set; } = null!;
    [Inject] private IAgmAttendanceSheetPdfRenderer AgmAttendanceSheetPdfRenderer { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private ILogger<EventList> Logger { get; set; } = null!;

    private bool _loading = true;
    private List<CombinedEventListItem> _events = new();
    private string _searchTerm = string.Empty;
    private string? _errorMessage;
    private string? _printMessage;

    private IEnumerable<CombinedEventListItem> DisplayItems =>
        string.IsNullOrWhiteSpace(_searchTerm)
            ? _events
            : _events.Where(item =>
                item.Date.ToString("d MMM yyyy").Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ||
                item.TypeName.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ||
                (item.Notes?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false));

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // The service already returns Date-descending order (FR-002) — no re-sort needed here.
            _events = (await CombinedEventListService.GetAllAsync()).ToList();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load events: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private void AddEvent() => Nav.NavigateTo("/events/new");

    private void RecordParticipation(Guid eventId) =>
        Nav.NavigateTo($"/events/{eventId}/participation");

    private async Task PrintAttendanceSheet(Guid eventId)
    {
        _printMessage = null;

        try
        {
            var sheetData = await EventAttendanceSheetService.GenerateAsync(eventId);

            if (sheetData.Members.Count == 0)
            {
                _printMessage = "No active members found — nothing to print.";
                return;
            }

            var settings = await SettingsService.GetAsync();
            var orgName = settings?.OrganizationName ?? string.Empty;
            var bytes = EventAttendanceSheetPdfRenderer.Render(sheetData, orgName);
            var tempPath = Path.Combine(Path.GetTempPath(), $"event-attendance-sheet_{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(tempPath, bytes);
#pragma warning disable CA1416
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath) { UseShellExecute = true });
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to print attendance sheet for event {EventId}", eventId);
            _printMessage = "Unable to print attendance sheet. Please try again.";
        }
    }

    private async Task PrintAgmAttendanceReport(Guid agmId)
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
