using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Reports.Rendering;

namespace StageFright.UI.Pages.Events;

public partial class EventDetail
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private IEventService EventService { get; set; } = null!;
    [Inject] private IEventAttendanceSheetService EventAttendanceSheetService { get; set; } = null!;
    [Inject] private IEventAttendanceSheetPdfRenderer EventAttendanceSheetPdfRenderer { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private ILogger<EventDetail> Logger { get; set; } = null!;

    private bool _loading = true;
    private Event? _event;
    private string? _errorMessage;
    private string? _printMessage;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _event = await EventService.GetByIdWithDetailsAsync(Id);
            if (_event is null)
                _errorMessage = "Event not found.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load event details: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task PrintAttendanceSheet()
    {
        _printMessage = null;

        try
        {
            var sheetData = await EventAttendanceSheetService.GenerateAsync(Id);

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
            Logger.LogError(ex, "Failed to print attendance sheet for event {EventId}", Id);
            _printMessage = "Unable to print attendance sheet. Please try again.";
        }
    }
}
