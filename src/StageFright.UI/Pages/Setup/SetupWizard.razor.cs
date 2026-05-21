using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using StageFright.Core.Services;
using System.ComponentModel.DataAnnotations;

namespace StageFright.UI.Pages.Setup;

public partial class SetupWizard : ComponentBase
{
    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required ISetupService SetupService { get; set; }

    [Inject]
    public required ILogger<SetupWizard> Logger { get; set; }

    private SetupModel setupModel = new();
    private bool setupComplete = false;
    private bool isLoading = false;
    private string errorMessage = string.Empty;

    private class SetupModel
    {
        [Required(ErrorMessage = "Organization name is required")]
        public string OrganizationName { get; set; } = string.Empty;

        [Range(0.01, 10000, ErrorMessage = "Annual fee must be between 0.01 and 10,000")]
        public decimal AnnualFee { get; set; } = 0;

        [Range(0.01, 10000, ErrorMessage = "Attendance fee must be between 0.01 and 10,000")]
        public decimal AttendanceFee { get; set; } = 0;

        [Range(1, 12, ErrorMessage = "Renewal month must be between 1 and 12")]
        public int RenewalMonth { get; set; } = 7;
    }

    protected override void OnInitialized()
    {
        setupModel.RenewalMonth = 7; // Default to July
    }

    private async Task HandleSetupAsync()
    {
        try
        {
            isLoading = true;
            errorMessage = string.Empty;

            await SetupService.InitializeApplicationAsync(
                setupModel.OrganizationName,
                setupModel.AnnualFee,
                setupModel.AttendanceFee,
                setupModel.RenewalMonth);

            setupComplete = true;
            isLoading = false;

            // Redirect to dashboard after a brief delay
            await Task.Delay(2000);
            Navigation.NavigateTo("/");
        }
        catch (Exception ex)
        {
            isLoading = false;
            errorMessage = $"Setup failed: {ex.Message}";
            Logger.LogError(ex, "Setup initialization failed");
        }
    }

    private void ResetForm()
    {
        setupModel = new();
        errorMessage = string.Empty;
        setupComplete = false;
    }

    private void GoToDashboard()
    {
        Navigation.NavigateTo("/");
    }

    private string GetMonthName(int month) => month switch
    {
        1 => "January",
        2 => "February",
        3 => "March",
        4 => "April",
        5 => "May",
        6 => "June",
        7 => "July",
        8 => "August",
        9 => "September",
        10 => "October",
        11 => "November",
        12 => "December",
        _ => "Unknown"
    };
}
