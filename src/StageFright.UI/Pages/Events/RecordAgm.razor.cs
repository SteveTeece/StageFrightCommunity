using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Agm;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Events;

public partial class RecordAgm : ComponentBase
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private IAgmService AgmService { get; set; } = null!;
    [Inject] private IMemberService MemberService { get; set; } = null!;
    [Inject] private ICommitteeOfficeHolderTypeService OfficeHolderTypeService { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<EventsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private AgmAttendanceGrid? _attendanceGrid;
    private AnnualGeneralMeeting? _agm;
    private List<Member> _activeMembers = [];
    private List<CommitteeOfficeHolderType> _officeHolderTypes = [];
    private Dictionary<Guid, Guid?> _officeHolderAssignments = new();
    private HashSet<Guid> _generalCommitteeMemberIds = [];
    private int? _seatCountTarget;
    private bool _loading = true;
    private bool _saving;
    private string? _guardMessage;
    private string? _errorMessage;

    private string MeetingDateText() =>
        Loc.Get<EventsResource>("Events_Agm_MeetingDateText", _agm!.Date.ToString("d MMMM yyyy"));

    private string SelectedCountText() =>
        Loc.Get<EventsResource>("Events_Agm_SelectedCount", _generalCommitteeMemberIds.Count);

    private string SelectedOfTargetText() =>
        Loc.Get<EventsResource>("Events_Agm_SelectedOfTarget", _seatCountTarget ?? 0);

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _guardMessage = null;

        _agm = await AgmService.GetByIdAsync(Id);
        if (_agm is null)
        {
            _guardMessage = L["Events_Agm_GuardNotFound"];
            _loading = false;
            return;
        }

        if (_agm.IsRecorded)
        {
            _guardMessage = L["Events_Agm_GuardAlreadyRecorded"];
            _loading = false;
            return;
        }

        if (_agm.Date.Date > DateTime.Today)
        {
            _guardMessage = L["Events_Agm_GuardFutureDate"];
            _loading = false;
            return;
        }

        _activeMembers = (await MemberService.GetByStatusAsync(MemberStatus.Active))
            .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .ToList();

        _officeHolderTypes = (await OfficeHolderTypeService.GetActiveAsync()).ToList();
        foreach (var type in _officeHolderTypes)
            _officeHolderAssignments[type.Id] = null;

        var settings = await SettingsService.GetAsync();
        _seatCountTarget = settings?.GeneralCommitteeSeatCountTarget;

        _loading = false;
    }

    private void OnOfficeHolderChanged(Guid officeHolderTypeId, string? memberIdText)
    {
        _officeHolderAssignments[officeHolderTypeId] =
            string.IsNullOrEmpty(memberIdText) ? null : Guid.Parse(memberIdText);
    }

    private void ToggleGeneralCommittee(Guid memberId, bool selected)
    {
        if (selected)
            _generalCommitteeMemberIds.Add(memberId);
        else
            _generalCommitteeMemberIds.Remove(memberId);
    }

    private async Task SaveAsync()
    {
        _errorMessage = null;

        var assignedOfficeHolders = _officeHolderAssignments
            .Where(kv => kv.Value.HasValue)
            .ToDictionary(kv => kv.Key, kv => kv.Value!.Value);

        var allAssignedMemberIds = assignedOfficeHolders.Values.Concat(_generalCommitteeMemberIds).ToList();
        if (allAssignedMemberIds.Count != allAssignedMemberIds.Distinct().Count())
        {
            _errorMessage = L["Events_Agm_DuplicateAssignmentError"];
            return;
        }

        _saving = true;
        try
        {
            var request = new RecordAgmRequest(
                _attendanceGrid?.GetAttendedMemberIds() ?? [],
                _activeMembers.Select(m => m.Id).ToList(),
                assignedOfficeHolders,
                _generalCommitteeMemberIds.ToList());

            var agm = await AgmService.RecordAsync(Id, request);
            Nav.NavigateTo($"/events/agm/{agm.Id}");
        }
        catch (ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<EventsResource>("Events_Agm_RecordSaveError", ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }
}
