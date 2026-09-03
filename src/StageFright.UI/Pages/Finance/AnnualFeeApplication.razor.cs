using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Finance;

public partial class AnnualFeeApplication
{
    [Inject] private IFeeService FeeService { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private bool _loading = true;
    private bool _confirmed;
    private bool _applying;
    private int _eligibleCount;
    private decimal _annualFee;
    private List<Guid> _eligibleMemberIds = new();
    private string? _errorMessage;
    private string? _successMessage;

    /// <summary>"N active member(s) are eligible…" — count-dependent wording.</summary>
    private string EligibleSummaryText() =>
        Loc.Plural<FinanceResource>("Finance_AnnualFees_EligibleSummary", _eligibleCount);

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
            _errorMessage = Loc.Get<FinanceResource>("Finance_AnnualFees_LoadError", ex.Message);
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
            _successMessage = Loc.Plural<FinanceResource>("Finance_AnnualFees_SuccessMessage", count);
        }
        catch (ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_AnnualFees_ApplyError", ex.Message);
        }
        finally
        {
            _applying = false;
        }
    }
}
