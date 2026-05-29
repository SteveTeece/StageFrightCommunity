using Microsoft.AspNetCore.Components;
using StageFright.Core.Services;
using System.Threading.Tasks;

namespace StageFright.UI.Pages.Finance;

public partial class AnnualFeeApplicationDialog
{
    [Inject]
    private IAnnualFeeApplicationService AnnualFeeService { get; set; } = null!;

    [Parameter]
    public EventCallback OnFeesApplied { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    private string? ErrorMessage;
    private bool LoadingCount = true;
    private bool IsApplying = false;
    private bool FeesApplied = false;
    private int EligibleMemberCount = 0;
    private int FeesAppliedCount = 0;
    private decimal AnnualFeeAmount = 0;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            EligibleMemberCount = await AnnualFeeService.GetEligibleMemberCountAsync();
            AnnualFeeAmount = await AnnualFeeService.GetAnnualFeeAmountAsync();
            LoadingCount = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading data: {ex.Message}";
            LoadingCount = false;
        }
    }

    private async Task ApplyFees()
    {
        try
        {
            ErrorMessage = null;
            IsApplying = true;

            FeesAppliedCount = await AnnualFeeService.ApplyAnnualFeesAsync();
            FeesApplied = true;

            await OnFeesApplied.InvokeAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            IsApplying = false;
        }
    }

    private async Task Cancel()
    {
        await OnCancel.InvokeAsync();
    }

    private async Task Close()
    {
        await OnFeesApplied.InvokeAsync();
    }
}
