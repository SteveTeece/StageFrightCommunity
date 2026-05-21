using Microsoft.AspNetCore.Components;
using StageFright.Core.Entities;
using StageFright.Core.Services;

namespace StageFright.UI.Pages.Members;

public partial class EditMemberForm : ComponentBase
{
    [Parameter]
    public required Guid MemberId { get; set; }

    [Parameter]
    public EventCallback OnSaved { get; set; }

    [Parameter]
    public EventCallback OnCancelled { get; set; }

    [Inject]
    public required IMemberService MemberService { get; set; }

    [Inject]
    public required MemberValidationService ValidationService { get; set; }

    [Inject]
    public required AgeCalculationService AgeService { get; set; }

    private Member? Member { get; set; }
    private Member FormModel = new();
    private string? ErrorMessage = null;
    private string DobString = "";
    private bool isCommitteeMember = false;
    private string CommitteePosition = "";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            Member = await MemberService.GetMemberByIdAsync(MemberId);
            if (Member == null)
            {
                ErrorMessage = "Member not found.";
                return;
            }

            // Copy to form model
            FormModel = new Member
            {
                Id = Member.Id,
                Name = Member.Name,
                StreetAddress = Member.StreetAddress,
                Phone = Member.Phone,
                Email = Member.Email,
                DateOfBirth = Member.DateOfBirth,
                Status = Member.Status,
                JoinDate = Member.JoinDate,
                ActivateDate = Member.ActivateDate,
                InactivateDate = Member.InactivateDate
            };

            if (Member.DateOfBirth.HasValue)
            {
                DobString = Member.DateOfBirth.Value.ToString("yyyy-MM-dd");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading member: {ex.Message}";
        }
    }

    private void HandleCommitteeMemberChange(ChangeEventArgs e)
    {
        if (e.Value is bool isCommittee)
        {
            isCommitteeMember = isCommittee;
            if (!isCommittee)
            {
                CommitteePosition = "";
            }
        }
    }

    private async Task SaveMember()
    {
        try
        {
            ErrorMessage = null;

            // Validate
            if (string.IsNullOrWhiteSpace(FormModel.Name))
            {
                ErrorMessage = "Name is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(FormModel.StreetAddress))
            {
                ErrorMessage = "Street address is required.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(DobString))
            {
                if (!DateTime.TryParse(DobString, out var dob))
                {
                    ErrorMessage = "Invalid date of birth.";
                    return;
                }

                // Validate age
                try
                {
                    AgeService.CalculateAge(dob);
                    FormModel.DateOfBirth = dob;
                }
                catch (ArgumentException ex)
                {
                    ErrorMessage = ex.Message;
                    return;
                }
            }
            else
            {
                FormModel.DateOfBirth = null;
            }

            // Submit update - server-side validation will occur in the service
            await MemberService.UpdateMemberAsync(FormModel);
            await OnSaved.InvokeAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error updating member: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error: {ex}");
        }
    }

    private async Task Cancel()
    {
        await OnCancelled.InvokeAsync();
    }
}
