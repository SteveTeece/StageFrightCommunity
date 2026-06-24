using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.UI.Pages.Events;

public partial class EventList
{
    [Inject] private IEventService EventService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private bool _loading = true;
    private List<Event> _events = new();
    private string _searchTerm = string.Empty;
    private string? _errorMessage;

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
}
