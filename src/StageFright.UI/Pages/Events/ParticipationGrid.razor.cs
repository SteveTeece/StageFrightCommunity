using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Events;
using StageFright.Core.Exceptions;

namespace StageFright.UI.Pages.Events;

public partial class ParticipationGrid
{
    [Parameter] public Guid EventId { get; set; }

    [Inject] private IEventService EventService { get; set; } = null!;
    [Inject] private IMemberService MemberService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private bool _loading = true;
    private bool _saving;
    private bool _selectAll;
    private bool _alreadyRecorded;
    private bool _isFutureDate;
    private string? _errorMessage;
    private Event? _event;
    private List<Member> _members = new();
    private List<ParticipationRow> _rows = new();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var all = await EventService.GetAllAsync();
            _event = all.FirstOrDefault(e => e.Id == EventId);

            if (_event is null)
            {
                _errorMessage = "Event not found.";
                return;
            }

            if (_event.StoredParticipationRate.HasValue)
            {
                _alreadyRecorded = true;
                return;
            }

            if (_event.Date.Date > DateTime.Today)
            {
                _isFutureDate = true;
                return;
            }

            var active = await MemberService.GetByStatusAsync(MemberStatus.Active);
            var inactive = await MemberService.GetByStatusAsync(MemberStatus.Inactive);
            _members = active.Concat(inactive).OrderBy(m => m.Name).ToList();

            _rows = _members.Select(m => new ParticipationRow
            {
                MemberId = m.Id,
                MemberName = m.Name,
                Participated = false
            }).ToList();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load participation grid: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private void ToggleSelectAll(ChangeEventArgs e)
    {
        _selectAll = (bool)(e.Value ?? false);
        foreach (var row in _rows)
            row.Participated = _selectAll;
    }

    private void SetRowParticipated(ParticipationRow row, bool participated)
    {
        row.Participated = participated;
        _selectAll = _rows.Count > 0 && _rows.All(r => r.Participated);
    }

    private async Task SaveParticipation()
    {
        _saving = true;
        _errorMessage = null;

        try
        {
            var items = _rows.Select(r => new ParticipationBatchItem
            {
                MemberId = r.MemberId,
                Participated = r.Participated
            }).ToList();

            await EventService.RecordParticipationAsync(EventId, items);
            Nav.NavigateTo("/events");
        }
        catch (ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to save participation: {ex.Message}";
        }
        finally
        {
            _saving = false;
        }
    }

    private sealed class ParticipationRow
    {
        public Guid MemberId { get; init; }
        public string MemberName { get; init; } = string.Empty;
        public bool Participated { get; set; }
    }
}
