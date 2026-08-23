using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Reports.Rendering;

namespace StageFright.UI.Pages.Events;

public partial class EventList
{
    [Inject] private IEventService EventService { get; set; } = null!;
    [Inject] private IEventAttendanceSheetService EventAttendanceSheetService { get; set; } = null!;
    [Inject] private IEventAttendanceSheetPdfRenderer EventAttendanceSheetPdfRenderer { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private ILogger<EventList> Logger { get; set; } = null!;

    private bool _loading = true;
    private List<Event> _events = new();
    private string _searchTerm = string.Empty;
    private string? _errorMessage;
    private string? _printMessage;

    private IEnumerable<Event> DisplayEvents =>
        string.IsNullOrWhiteSpace(_searchTerm)
            ? _events
            : _events.Where(e =>
                e.Date.ToString("d MMM yyyy").Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ||
                (e.EventType?.Name?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.Notes?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false));

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var result = await EventService.GetAllAsync();
            _events = result
                .OrderByDescending(e => e.Date)
                .ToList();
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
}
