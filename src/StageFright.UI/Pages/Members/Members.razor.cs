using Microsoft.AspNetCore.Components;
using StageFright.Core.Services;
using StageFright.Core.Entities;

namespace StageFright.UI.Pages.Members;

public partial class Members
{
    [Inject]
    public IMemberService MemberService { get; set; } = default!;

    private List<Member> ActiveMembers = new();
    private List<Member> InactiveMembers = new();
    private bool IsLoading = true;
    private bool ShowForm = false;
    private string StatusFilter = "Active";
    private string? ErrorMessage = null;

    private List<Member> DisplayedMembers
    {
        get
        {
            return StatusFilter == "Active" ? ActiveMembers : InactiveMembers;
        }
    }

    private AgeCalculationService AgeService = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadMembers();
    }

    private async Task LoadMembers()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var active = await MemberService.GetActiveMembersAsync();
            var inactive = await MemberService.GetInactiveMembersAsync();

            ActiveMembers = active.ToList();
            InactiveMembers = inactive.ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading members: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ShowAddForm() => ShowForm = true;

    private void HideForm()
    {
        ShowForm = false;
        StateHasChanged();
    }

    private async Task MemberAdded()
    {
        HideForm();
        await LoadMembers();
    }

    private void EditMember(Member member)
    {
        // TODO: Show edit form
    }

    private async Task InactivateMember(Guid memberId)
    {
        try
        {
            await MemberService.InactivateMemberAsync(memberId);
            await LoadMembers();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error inactivating member: {ex.Message}";
        }
    }

    private async Task ActivateMember(Guid memberId)
    {
        try
        {
            await MemberService.ActivateMemberAsync(memberId);
            await LoadMembers();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error activating member: {ex.Message}";
        }
    }

    private void SetStatusFilter(string status)
    {
        StatusFilter = status;
    }

    private string GetMemberAge(Member member)
    {
        if (!member.DateOfBirth.HasValue)
            return "N/A";

        try
        {
            return AgeService.CalculateAge(member.DateOfBirth.Value).ToString();
        }
        catch
        {
            return "N/A";
        }
    }
}
