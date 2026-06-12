using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using StageFright.Core.Modules.Settings;

namespace StageFright.UI.Pages.Setup;

public partial class SetupWizard : ComponentBase
{
    private readonly SetupFormModel _model = new();
    private bool _submitting;
    private string? _errorMessage;

    private async Task HandleValidSubmitAsync()
    {
        _submitting = true;
        _errorMessage = null;

        try
        {
            var request = new SetupRequest(
                OrganizationName: _model.OrganizationName!,
                AnnualFee: _model.AnnualFee,
                AttendanceFee: _model.AttendanceFee,
                MembershipRenewalMonth: _model.MembershipRenewalMonth);

            await SetupService.InitializeAsync(request);
            Nav.NavigateTo("/dashboard");
        }
        catch (Core.Exceptions.ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch
        {
            _errorMessage = "An unexpected error occurred. Please try again.";
        }
        finally
        {
            _submitting = false;
        }
    }

    internal sealed class SetupFormModel
    {
        [Required(ErrorMessage = "Organisation name is required.")]
        [StringLength(255, ErrorMessage = "Organisation name must not exceed 255 characters.")]
        public string? OrganizationName { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Annual fee must be zero or greater.")]
        public decimal AnnualFee { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Attendance fee must be zero or greater.")]
        public decimal AttendanceFee { get; set; }

        [Range(1, 12, ErrorMessage = "Renewal month must be between 1 and 12.")]
        public int MembershipRenewalMonth { get; set; } = 1;
    }
}
