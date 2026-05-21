using Microsoft.AspNetCore.Components;
using StageFright.Data.Repositories;
using StageFright.Core.Entities;

namespace StageFright.UI.Pages.Events;

public partial class ScheduleEventForm
{
    [Parameter]
    public EventCallback OnSaved { get; set; }

    [Parameter]
    public EventCallback OnCancelled { get; set; }

    [Inject]
    public IEventRepository EventRepository { get; set; } = default!;

    private string DateString = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
    private string EventType = "";
    private string Notes = "";
    private string? ErrorMessage = null;

    private async Task ScheduleEvent()
    {
        try
        {
            ErrorMessage = null;

            if (!DateTime.TryParse(DateString, out var date))
            {
                ErrorMessage = "Invalid date.";
                return;
            }

            if (string.IsNullOrWhiteSpace(EventType))
            {
                ErrorMessage = "Event type is required.";
                return;
            }

            var evt = new Event
            {
                Id = Guid.NewGuid(),
                Date = date,
                EventType = EventType,
                Notes = Notes,
                StoredParticipationRate = 0
            };

            await EventRepository.CreateAsync(evt);
            await OnSaved.InvokeAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error scheduling event: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error: {ex}");
        }
    }

    private async Task Cancel()
    {
        await OnCancelled.InvokeAsync();
    }
}
