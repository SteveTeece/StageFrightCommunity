using Microsoft.AspNetCore.Components;

namespace StageFright.UI.Pages.Finance;

public partial class Finance
{
    private string ActiveTab = "payments";
    private bool ShowingAnnualFeeDialog = false;

    private void HandlePaymentRecorded()
    {
        // Refresh the member balances tab after payment is recorded
        StateHasChanged();
    }

    private void HandleFormCancel()
    {
        // Handle cancel action
        StateHasChanged();
    }

    private void HandleFeesApplied()
    {
        // Close the dialog and reset
        ShowingAnnualFeeDialog = false;
        StateHasChanged();
    }

    private void HandleAnnualFeeCancel()
    {
        // Close the dialog
        ShowingAnnualFeeDialog = false;
        StateHasChanged();
    }
}
