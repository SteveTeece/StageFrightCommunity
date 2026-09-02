using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Finance;

public partial class RecordIncome : ComponentBase
{
    [Inject] private IIncomeEntryService IncomeEntryService { get; set; } = null!;
    [Inject] private IAccountService AccountService { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private readonly RecordIncomeModel _form = new();
    private readonly Dictionary<string, string> _errors = new();
    private IReadOnlyList<Account> _accounts = [];
    private IReadOnlyList<Account> _bankAccounts = [];
    private bool _loading = true;
    private bool _saving;
    private bool _isTaxApplicable;
    private decimal _taxRate;
    private int _minorUnitDigits = 2;
    private TaxEntryMode _taxEntryMode = TaxEntryMode.Inclusive;
    private string? _successMessage;
    private string? _errorMessage;

    // The Amount field is entered gross under Inclusive mode and net under Exclusive mode (issue #354).
    private string AmountLabel =>
        !_isTaxApplicable ? L["Finance_Common_AmountLabel"]
        : _taxEntryMode == TaxEntryMode.Exclusive ? L["Finance_Common_AmountLabelTaxExclusive"]
        : L["Finance_Common_AmountLabelTaxInclusive"];

    private string? TaxHint
    {
        get
        {
            if (!_isTaxApplicable || _form.TaxCode != TaxCode.Taxable || _form.Amount <= 0m)
                return null;

            var (gross, _, tax) = TaxCalculator.Split(_form.Amount, _taxEntryMode, _taxRate, _minorUnitDigits);
            return _taxEntryMode == TaxEntryMode.Exclusive
                ? Loc.Get<FinanceResource>("Finance_Common_TaxExclusiveHint",
                    MoneyFormatter.Format(tax), MoneyFormatter.Format(gross))
                : Loc.Get<FinanceResource>("Finance_Common_TaxInclusiveHint", MoneyFormatter.Format(tax));
        }
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _accounts = await IncomeEntryService.GetIncomeAccountsAsync();
            if (_accounts.Count == 1)
                _form.AccountId = _accounts[0].Id;

            _bankAccounts = await AccountService.GetBankAccountsAsync();
            _form.DepositAccountId = SystemAccounts.CashId;

            var settings = await SettingsService.GetAsync();
            _isTaxApplicable = settings?.IsTaxApplicable ?? false;
            _taxRate = settings?.TaxRate ?? 0m;
            _minorUnitDigits = CurrencyCatalog.Get(settings?.CurrencyCode ?? CurrencyCatalog.Default.Code).MinorUnitDigits;
            _taxEntryMode = settings?.TaxEntryMode ?? TaxEntryMode.Inclusive;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_RecordIncome_LoadAccountsError", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task SaveAsync()
    {
        _errors.Clear();
        _errorMessage = null;

        if (_form.Amount <= 0m)
        {
            _errors["Amount"] = L["Finance_Common_AmountPositiveError"];
            return;
        }

        if (_form.AccountId == Guid.Empty)
        {
            _errors["AccountId"] = L["Finance_RecordIncome_AccountRequiredError"];
            return;
        }

        _saving = true;
        try
        {
            var request = new RecordIncomeRequest
            {
                Date = _form.Date,
                Amount = _form.Amount,
                AccountId = _form.AccountId,
                DepositAccountId = _form.DepositAccountId == Guid.Empty ? null : _form.DepositAccountId,
                TaxCode = _isTaxApplicable ? _form.TaxCode : null,
                Description = string.IsNullOrWhiteSpace(_form.Description) ? null : _form.Description.Trim()
            };

            await IncomeEntryService.RecordIncomeAsync(request);
            _successMessage = Loc.Get<FinanceResource>("Finance_RecordIncome_SuccessMessage",
                MoneyFormatter.Format(request.Amount));
        }
        catch (ClosedPeriodException)
        {
            _errorMessage = Loc.Get<ValidationResource>("Validation_ClosedPeriod_PostingRejected");
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_RecordIncome_RecordError", ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    private void RecordAnother()
    {
        _successMessage = null;
        _errorMessage = null;
        _form.Amount = 0m;
        _form.TaxCode = null;
        _form.Description = null;
        _form.Date = DateTime.Today;
    }
}
