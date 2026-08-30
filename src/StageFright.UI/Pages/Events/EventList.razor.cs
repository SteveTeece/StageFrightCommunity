using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Events;
using StageFright.Reports.Rendering;
using StageFright.UI.Resources.Strings;

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
    [Inject] private IStringLocalizer<EventsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

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

    private string NoMatchText() => Loc.Get<EventsResource>("Events_List_NoMatch", _searchTerm);

    private string PrintAgmReportAriaLabel(DateTime date) =>
        Loc.Get<EventsResource>("Events_List_PrintAgmReportAriaLabel", date.ToString("d MMM yyyy"));

    private string RecordParticipationAriaLabel(DateTime date) =>
        Loc.Get<EventsResource>("Events_List_RecordParticipationAriaLabel", date.ToString("d MMM yyyy"));

    private string PrintSheetAriaLabel(DateTime date) =>
        Loc.Get<EventsResource>("Events_List_PrintSheetAriaLabel", date.ToString("d MMM yyyy"));

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // The service already returns Date-descending order (FR-002) — no re-sort needed here.
            _events = (await CombinedEventListService.GetAllAsync()).ToList();
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<EventsResource>("Events_List_LoadError", ex.Message);
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
                _printMessage = L["Events_List_PrintNoMembers"];
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
            _printMessage = L["Events_List_PrintSheetError"];
        }
    }

    private async Task PrintAgmAttendanceReport(Guid agmId) =>
        _printMessage = await AgmAttendanceReportPrinter.PrintAsync(
            agmId, AgmAttendanceSheetService, AgmAttendanceSheetPdfRenderer, SettingsService, Logger, L);
}
