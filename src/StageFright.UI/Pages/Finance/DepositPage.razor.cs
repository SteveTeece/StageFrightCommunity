using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Finance;

/// <summary>Record bank deposit (spec 033, US3): tick cheques, enter cash, see the split total, save.</summary>
public partial class DepositPage : ComponentBase
{
    private const string BankingPath = "/finance/banking";

    [Inject] private IBankingService BankingService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private readonly DepositModel _form = new();
    private DepositItemSelectionGrid? _grid;
    private IReadOnlyList<Account> _banks = [];
    private IReadOnlyList<AwaitingDepositItem> _items = [];
    private decimal _cashAvailable;
    private decimal _chequeTotal;
    private bool _loading = true;
    private bool _saving;
    private string? _errorMessage;

    private string CashAvailableHelp =>
        Loc.Get<FinanceResource>("Finance_Banking_CashAvailableHelp", MoneyFormatter.Format(_cashAvailable));

    private decimal CashAmount => _form.CashAmount ?? 0m;

    private decimal DepositTotal => _chequeTotal + CashAmount;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var overview = await BankingService.GetOverviewAsync();
            _banks = overview.DepositBankAccounts;
            _items = overview.AwaitingItems;
            _cashAvailable = overview.CashAvailableToday;

            if (_banks.Count == 1)
                _form.BankAccountId = _banks[0].Id;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_Banking_LoadError", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnSelectionChanged(decimal chequeTotal) => _chequeTotal = chequeTotal;

    // FR-010: the cash available depends on the deposit date.
    private async Task OnDateChangedAsync()
    {
        try
        {
            _cashAvailable = await BankingService.GetCashAvailableAsync(_form.Date);
            if (_cashAvailable <= 0m)
                _form.CashAmount = null;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_Banking_LoadError", ex.Message);
        }
    }

    private async Task SaveAsync()
    {
        // A second submit while the first is in flight would post a cash-only deposit twice.
        if (_saving)
            return;

        _errorMessage = null;
        _saving = true;
        try
        {
            await BankingService.RecordDepositAsync(new RecordDepositRequest
            {
                Date = _form.Date,
                BankAccountId = _form.BankAccountId,
                ReceivedItemIds = _grid?.GetSelectedItemIds() ?? [],
                CashAmount = CashAmount
            });
            Nav.NavigateTo(BankingPath);
        }
        catch (ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (ClosedPeriodException)
        {
            _errorMessage = Loc.Get<ValidationResource>("Validation_ClosedPeriod_PostingRejected");
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_Banking_DepositSaveError", ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => Nav.NavigateTo(BankingPath);
}
