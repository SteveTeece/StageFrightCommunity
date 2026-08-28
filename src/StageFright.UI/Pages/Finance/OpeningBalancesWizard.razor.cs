using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Finance;

public partial class OpeningBalancesWizard : ComponentBase
{
    [Inject] private IOpeningBalanceService OpeningBalanceService { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private IReadOnlyList<Account> _accounts = [];
    private int _step = 1;
    private DateTime _asAtDate = DateTime.Today;
    private bool _hasExistingOpeningBalances;
    private bool _loading = true;
    private string? _successMessage;
    private string? _errorMessage;

    /// <summary>"Step N of 2" wizard progress caption.</summary>
    private string StepText() =>
        Loc.Get<FinanceResource>("Finance_OpeningBalances_StepOf", _step);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _accounts = await OpeningBalanceService.GetOpeningBalanceAccountsAsync();

            _hasExistingOpeningBalances = await OpeningBalanceService.HasExistingOpeningBalancesAsync();

            var settings = await SettingsService.GetAsync();
            var startMonth = settings?.FinancialYearStartMonth ?? FinancialYearCalculator.DefaultStartMonth;
            var (fyStart, _) = FinancialYearCalculator.GetRange(DateTime.Today, startMonth);
            _asAtDate = fyStart;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_OpeningBalances_LoadError", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private void GoToStep(int step) => _step = step;

    private async Task PostAsync(RecordOpeningBalancesRequest request)
    {
        _errorMessage = null;
        try
        {
            await OpeningBalanceService.RecordOpeningBalancesAsync(request);
            _successMessage = Loc.Get<FinanceResource>("Finance_OpeningBalances_SuccessMessage",
                request.AsAtDate.ToString("d MMMM yyyy"));
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_OpeningBalances_PostError", ex.Message);
        }
    }
}
