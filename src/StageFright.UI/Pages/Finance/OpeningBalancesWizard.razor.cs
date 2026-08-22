using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Finance;

namespace StageFright.UI.Pages.Finance;

public partial class OpeningBalancesWizard : ComponentBase
{
    [Inject] private IOpeningBalanceService OpeningBalanceService { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;

    private IReadOnlyList<Account> _accounts = [];
    private int _step = 1;
    private DateTime _asAtDate = DateTime.Today;
    private bool _hasExistingOpeningBalances;
    private bool _loading = true;
    private string? _successMessage;
    private string? _errorMessage;

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
            _errorMessage = $"Failed to load accounts: {ex.Message}";
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
            _successMessage = $"Opening balances as at {request.AsAtDate:d MMMM yyyy} posted successfully.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to post opening balances: {ex.Message}";
        }
    }
}
