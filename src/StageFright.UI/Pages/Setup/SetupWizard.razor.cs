using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Settings;

namespace StageFright.UI.Pages.Setup;

public partial class SetupWizard : ComponentBase
{
    [Inject] private IDebugDataSeeder DebugSeeder { get; set; } = null!;

    private readonly SetupFormModel _model = new();
    private bool _submitting;
    private bool _seedWithTestData;
    private string? _errorMessage;
    private string? _seedingProgress;

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

            if (_seedWithTestData)
            {
                var progress = new Progress<string>(msg =>
                {
                    _seedingProgress = msg;
                    InvokeAsync(StateHasChanged);
                });
                await Task.Run(() => DebugSeeder.SeedAsync(progress));
            }

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
