using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Events;
using StageFright.Core.Exceptions;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Events;

public partial class ParticipationGrid
{
    [Parameter] public Guid EventId { get; set; }

    [Inject] private IEventService EventService { get; set; } = null!;
    [Inject] private IMemberService MemberService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<EventsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private bool _loading = true;
    private bool _saving;
    private bool _selectAll;
    private bool _alreadyRecorded;
    private bool _isFutureDate;
    private string? _errorMessage;
    private Event? _event;
    private List<Member> _members = new();
    private List<ParticipationRow> _rows = new();

    private string SubtitleText() =>
        Loc.Get<EventsResource>("Events_Participation_Subtitle",
            _event!.Date.ToString("dddd, d MMMM yyyy"), _event.EventType?.Name ?? L["Events_Detail_PageTitleFallback"].Value);

    private string FutureNoticeText() =>
        Loc.Get<EventsResource>("Events_Participation_FutureNotice",
            _event?.Date.ToString("dddd, d MMMM yyyy") ?? string.Empty);

    private string RowAriaLabel(string memberName) =>
        Loc.Get<EventsResource>("Events_Participation_RowAriaLabel", memberName);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var all = await EventService.GetAllAsync();
            _event = all.FirstOrDefault(e => e.Id == EventId);

            if (_event is null)
            {
                _errorMessage = L["Events_Participation_NotFoundError"];
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
            _members = active.Concat(inactive).OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ToList();

            _rows = _members.Select(m => new ParticipationRow
            {
                MemberId = m.Id,
                MemberName = m.SortableFullName,
                Participated = false
            }).ToList();
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<EventsResource>("Events_Participation_LoadError", ex.Message);
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
            _errorMessage = Loc.Get<EventsResource>("Events_Participation_SaveError", ex.Message);
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
