using Microsoft.AspNetCore.Components;
using StageFright.Core.Entities;
using StageFright.Core.Services;

namespace StageFright.UI.Pages.Members;

public partial class AddMemberForm : ComponentBase
{
    [Parameter]
    public required EventCallback OnSaved { get; set; }

    [Parameter]
    public required EventCallback OnCancelled { get; set; }

    [Inject]
    public required IMemberService MemberService { get; set; }

    [Inject]
    public required MemberValidationService ValidationService { get; set; }

    [Inject]
    public required AgeCalculationService AgeService { get; set; }

    private Member FormModel = new Member();
    private string? ErrorMessage = null;
    private string JoinDateString = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
    private string DobString = "";

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

            if (!DateTime.TryParse(JoinDateString, out var joinDate))
            {
                ErrorMessage = "Invalid join date.";
                return;
            }

            FormModel.JoinDate = joinDate;
            FormModel.Status = "Active";
            FormModel.ActivateDate = joinDate;

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

            // Submit creation - server-side validation will occur in the service
            FormModel.Id = Guid.NewGuid();

            await MemberService.CreateMemberAsync(FormModel);
            await OnSaved.InvokeAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error adding member: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error: {ex}");
        }
    }

    private async Task Cancel()
    {
        await OnCancelled.InvokeAsync();
    }
}
