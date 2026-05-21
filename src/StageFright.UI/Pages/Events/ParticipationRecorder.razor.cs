using Microsoft.AspNetCore.Components;
using StageFright.Data.Repositories;
using StageFright.Core.Entities;

namespace StageFright.UI.Pages.Events;

public partial class ParticipationRecorder
{
    [Parameter]
    public Guid EventId { get; set; }

    [Parameter]
    public EventCallback OnSaved { get; set; }

    [Parameter]
    public EventCallback OnCancelled { get; set; }

    [Inject]
    public IEventRepository EventRepository { get; set; } = default!;

    [Inject]
    public IMemberRepository MemberRepository { get; set; } = default!;

    [Inject]
    public IParticipationRepository ParticipationRepository { get; set; } = default!;

    private Event? Event { get; set; }
    private List<Member> Members = new();
    private List<ParticipationRecord> ParticipationRecords = new();
    private bool IsLoading = true;
    private string? ErrorMessage = null;

    private bool _allParticipated = false;
    private bool AllParticipated
    {
        get => _allParticipated;
        set
        {
            _allParticipated = value;
            foreach (var record in ParticipationRecords)
            {
                record.Participated = value;
            }
        }
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            Event = await EventRepository.GetByIdAsync(EventId);
            if (Event == null)
            {
                ErrorMessage = "Event not found.";
                return;
            }

            var activeMembers = await MemberRepository.GetActiveMembersAsync();
            Members = activeMembers.OrderBy(m => m.Name).ToList();

            ParticipationRecords = Members.Select(m => new ParticipationRecord
            {
                MemberId = m.Id,
                Participated = false
            }).ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading participation recorder: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveParticipation()
    {
        try
        {
            ErrorMessage = null;

            if (Event == null)
            {
                ErrorMessage = "Event not found.";
                return;
            }

            // Calculate participation rate
            var participatingMembers = ParticipationRecords.Where(p => p.Participated).ToList();
            var participationRate = Members.Count > 0 ? (decimal)participatingMembers.Count / Members.Count * 100 : 0;

            // Record participation for all members
            foreach (var record in ParticipationRecords.Where(p => p.Participated))
            {
                await ParticipationRepository.RecordAsync(EventId, record.MemberId);
            }

            // Update event with stored participation rate
            Event.StoredParticipationRate = participationRate;
            Event.IsDeleted = false;
            await EventRepository.UpdateAsync(Event);

            await OnSaved.InvokeAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving participation: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error: {ex}");
        }
    }

    private async Task Cancel()
    {
        await OnCancelled.InvokeAsync();
    }

    private class ParticipationRecord
    {
        public Guid MemberId { get; set; }
        public bool Participated { get; set; }
    }
}
