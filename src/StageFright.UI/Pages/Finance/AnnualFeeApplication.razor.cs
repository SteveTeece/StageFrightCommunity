using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;

namespace StageFright.UI.Pages.Finance;

public partial class AnnualFeeApplication
{
    [Inject] private IFeeService FeeService { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private bool _loading = true;
    private bool _confirmed;
    private bool _applying;
    private int _eligibleCount;
    private decimal _annualFee;
    private List<Guid> _eligibleMemberIds = new();
    private string? _errorMessage;
    private string? _successMessage;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var settings = await SettingsService.GetAsync();
            _annualFee = settings?.AnnualFee ?? 0m;

            var eligible = await FeeService.GetEligibleMembersAsync();
            _eligibleCount = eligible.Count;
            _eligibleMemberIds = eligible.Select(m => m.Id).ToList();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load eligible members: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ConfirmApply()
    {
        _applying = true;
        _errorMessage = null;

        try
        {
            var count = await FeeService.ApplyAnnualFeesAsync(_eligibleMemberIds);
            _confirmed = true;
            _successMessage = $"Annual fees applied successfully to {count} {(count == 1 ? "member" : "members")}.";
        }
        catch (ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to apply annual fees: {ex.Message}";
        }
        finally
        {
            _applying = false;
        }
    }
}
