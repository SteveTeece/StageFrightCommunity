using System.Globalization;
using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Finance;

namespace StageFright.UI.Pages.Finance;

public partial class OpeningBalancesWizard : ComponentBase
{
    [Inject] private IOpeningBalanceService OpeningBalanceService { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;

    private readonly List<OpeningBalanceRowModel> _rows = new();
    private IReadOnlyList<Account> _accounts = [];
    private int _step = 1;
    private DateTime _asAtDate = DateTime.Today;
    private bool _hasExistingOpeningBalances;
    private bool _loading = true;
    private bool _saving;
    private string? _successMessage;
    private string? _errorMessage;

    private IReadOnlyList<OpeningBalanceRowModel> NonZeroRows =>
        _rows.Where(r => r.Amount != 0m).ToList();

    private decimal Plug => OpeningBalanceService.ComputePlug(
        _rows.Select(r => new OpeningBalanceEntry { AccountId = r.Account.Id, Amount = r.Amount }).ToList(),
        _accounts);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _accounts = await OpeningBalanceService.GetOpeningBalanceAccountsAsync();
            _rows.AddRange(_accounts.Select(a => new OpeningBalanceRowModel { Account = a }));

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

    private void SetAmount(OpeningBalanceRowModel row, ChangeEventArgs args)
    {
        row.Amount = decimal.TryParse(
            args.Value?.ToString(), NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
            ? amount
            : 0m;
    }

    private async Task PostAsync()
    {
        _errorMessage = null;
        _saving = true;
        try
        {
            var request = new RecordOpeningBalancesRequest
            {
                AsAtDate = _asAtDate,
                Entries = NonZeroRows
                    .Select(r => new OpeningBalanceEntry { AccountId = r.Account.Id, Amount = r.Amount })
                    .ToList()
            };

            await OpeningBalanceService.RecordOpeningBalancesAsync(request);
            _successMessage = $"Opening balances as at {_asAtDate:d MMMM yyyy} posted successfully.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to post opening balances: {ex.Message}";
        }
        finally
        {
            _saving = false;
        }
    }
}
