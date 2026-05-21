using Microsoft.AspNetCore.Components;
using StageFright.Data.Repositories;
using StageFright.Core.Entities;

namespace StageFright.UI.Pages.Events;

public partial class Events
{
    [Inject]
    public IEventRepository EventRepository { get; set; } = default!;

    [Inject]
    public IMemberRepository MemberRepository { get; set; } = default!;

    private List<Event> EventList = new();
    private bool IsLoading = true;
    private bool ShowScheduleForm = false;
    private bool ShowParticipationRecorder = false;
    private Guid SelectedEventId = Guid.Empty;
    private string? ErrorMessage = null;

    protected override async Task OnInitializedAsync()
    {
        await LoadEvents();
    }

    private async Task LoadEvents()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var events = await EventRepository.GetAllAsync();
            EventList = events.OrderByDescending(e => e.Date).ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading events: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenScheduleForm() => ShowScheduleForm = true;
    private void HideScheduleForm() => ShowScheduleForm = false;
    private void HideParticipationRecorder() => ShowParticipationRecorder = false;

    private async Task EventScheduled()
    {
        HideScheduleForm();
        await LoadEvents();
    }

    private async Task ParticipationRecorded()
    {
        HideParticipationRecorder();
        await LoadEvents();
    }

    private void RecordParticipation(Guid eventId)
    {
        SelectedEventId = eventId;
        ShowParticipationRecorder = true;
    }
}
