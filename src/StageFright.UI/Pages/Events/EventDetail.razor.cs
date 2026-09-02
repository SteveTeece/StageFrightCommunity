using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Localization;
using StageFright.Reports.Rendering;
using StageFright.UI.Resources.Strings;

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
    [Inject] private IStringLocalizer<EventsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private bool _loading = true;
    private Event? _event;
    private string? _errorMessage;
    private string? _printMessage;

    /// <summary>Browser tab title — the event date, or a fallback before it loads.</summary>
    private string PageTitleText() =>
        $"{(_event is not null ? _event.Date.ToString("d MMM yyyy") : L["Events_Detail_PageTitleFallback"].Value)} — StageFright Community";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _event = await EventService.GetByIdWithDetailsAsync(Id);
            if (_event is null)
                _errorMessage = L["Events_Detail_NotFoundError"];
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<EventsResource>("Events_Detail_LoadError", ex.Message);
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
                _printMessage = L["Events_Detail_PrintNoMembers"];
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
            _printMessage = L["Events_Detail_PrintSheetError"];
        }
    }
}
