using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.UI.Pages.Events;

public partial class EventDetail
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private IEventService EventService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private bool _loading = true;
    private Event? _event;
    private string? _errorMessage;

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
}
