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

/// <summary>
/// Records a mid-term replacement against the currently-open committee term (FR-026–FR-028):
/// close out a departing office holder/general committee member with an end date and open a
/// new record for the incoming member, without running a full AGM.
/// </summary>
public partial class RecordSpecialElection : ComponentBase
{
    [Inject] private IAgmService AgmService { get; set; } = null!;
    [Inject] private ICommitteeService CommitteeService { get; set; } = null!;
    [Inject] private IMemberService MemberService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<EventsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private List<CommitteePositionRecord> _currentPositions = [];
    private List<Member> _activeMembers = [];
    private Guid? _selectedOutgoingId;
    private Guid? _selectedIncomingMemberId;
    private DateTime _replacementDate = DateTime.Today;
    private bool _loading = true;
    private bool _saving;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        var current = await CommitteeService.GetCurrentAsync();
        _currentPositions = current
            .Where(p => p.EndDate is null)
            .OrderBy(p => p.OfficeHolderType is null)
            .ThenBy(p => p.OfficeHolderType?.DisplayOrder)
            .ToList();

        _activeMembers = (await MemberService.GetByStatusAsync(MemberStatus.Active))
            .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .ToList();

        _loading = false;
    }

    private void OnOutgoingChanged(string? value) =>
        _selectedOutgoingId = string.IsNullOrEmpty(value) ? null : Guid.Parse(value);

    private void OnIncomingChanged(string? value) =>
        _selectedIncomingMemberId = string.IsNullOrEmpty(value) ? null : Guid.Parse(value);

    private string DescribePosition(CommitteePositionRecord record) =>
        Loc.Get<EventsResource>("Events_SpecialElection_PositionDescription",
            record.OfficeHolderType?.Name ?? L["Events_SpecialElection_PositionFallback"].Value,
            record.Member.SortableFullName);

    private async Task SaveAsync()
    {
        _errorMessage = null;

        if (_selectedOutgoingId is null || _selectedIncomingMemberId is null)
        {
            _errorMessage = L["Events_SpecialElection_ValidationSelectBoth"];
            return;
        }

        _saving = true;
        try
        {
            var request = new RecordSpecialElectionRequest(
                _selectedOutgoingId.Value, _selectedIncomingMemberId.Value, _replacementDate);

            await AgmService.RecordSpecialElectionAsync(request);
            Nav.NavigateTo("/events/agm");
        }
        catch (ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (DataIntegrityException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<EventsResource>("Events_SpecialElection_SaveError", ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }
}
