using Microsoft.AspNetCore.Components;
using StageFright.Core.Entities;
using StageFright.Core.Services;

namespace StageFright.UI.Pages.Members;

public partial class CommitteeHistorySection : ComponentBase
{
    [Parameter]
    public required Guid MemberId { get; set; }

    [Inject]
    public required ICommitteeMembershipService CommitteeMembershipService { get; set; }

    private IEnumerable<CommitteeMembership>? CommitteeHistory { get; set; }
    private bool IsLoading = true;
    private string? ErrorMessage = null;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            CommitteeHistory = await CommitteeMembershipService.GetMemberCommitteeHistoryAsync(MemberId);
            IsLoading = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading committee history: {ex.Message}";
            IsLoading = false;
        }
    }
}
