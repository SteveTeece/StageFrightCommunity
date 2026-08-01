using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Agm;

namespace StageFright.UI.Pages.Events;

public partial class RecordAgm : ComponentBase
{
    [Inject] private IAgmService AgmService { get; set; } = null!;
    [Inject] private IMemberService MemberService { get; set; } = null!;
    [Inject] private ICommitteeOfficeHolderTypeService OfficeHolderTypeService { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private AgmAttendanceGrid? _attendanceGrid;
    private DateTime _date = DateTime.Today;
    private string? _notes;
    private List<Member> _activeMembers = [];
    private List<CommitteeOfficeHolderType> _officeHolderTypes = [];
    private Dictionary<Guid, Guid?> _officeHolderAssignments = new();
    private HashSet<Guid> _generalCommitteeMemberIds = [];
    private int? _seatCountTarget;
    private bool _loading = true;
    private bool _saving;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
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
            _errorMessage = "A member cannot hold more than one committee assignment from the same AGM.";
            return;
        }

        _saving = true;
        try
        {
            var request = new RecordAgmRequest(
                _date,
                _notes,
                _attendanceGrid?.GetAttendedMemberIds() ?? [],
                _activeMembers.Select(m => m.Id).ToList(),
                assignedOfficeHolders,
                _generalCommitteeMemberIds.ToList());

            var agm = await AgmService.RecordAsync(request);
            Nav.NavigateTo($"/events/agm/{agm.Id}");
        }
        catch (ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to save AGM: {ex.Message}";
        }
        finally
        {
            _saving = false;
        }
    }
}
