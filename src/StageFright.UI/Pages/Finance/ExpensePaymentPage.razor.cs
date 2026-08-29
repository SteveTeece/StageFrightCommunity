using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Finance;

public partial class ExpensePaymentPage : ComponentBase
{
    [Inject] private IExpensePaymentService ExpensePaymentService { get; set; } = null!;
    [Inject] private IAccountService AccountService { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private readonly ExpensePaymentModel _form = new();
    private readonly Dictionary<string, string> _errors = new();
    private IReadOnlyList<Account> _expenseAccounts = [];
    private IReadOnlyList<Account> _bankAccounts = [];
    private bool _loading = true;
    private bool _saving;
    private bool _isTaxApplicable;
    private decimal _taxRate;
    private int _minorUnitDigits = 2;
    private string? _successMessage;
    private string? _errorMessage;

    private string? TaxInclusiveHint =>
        _isTaxApplicable && _form.TaxCode == TaxCode.Taxable && _form.Amount > 0m
            ? Loc.Get<FinanceResource>("Finance_Common_TaxInclusiveHint",
                MoneyFormatter.Format(TaxCalculator.SplitInclusive(_form.Amount, _taxRate, _minorUnitDigits).Tax))
            : null;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _expenseAccounts = await ExpensePaymentService.GetExpenseAccountsAsync();
            _bankAccounts = await AccountService.GetBankAccountsAsync();

            if (_expenseAccounts.Count == 1)
                _form.ExpenseAccountId = _expenseAccounts[0].Id;
            if (_bankAccounts.Count == 1)
                _form.BankAccountId = _bankAccounts[0].Id;

            var settings = await SettingsService.GetAsync();
            _isTaxApplicable = settings?.IsTaxApplicable ?? false;
            _taxRate = settings?.TaxRate ?? 0m;
            _minorUnitDigits = CurrencyCatalog.Get(settings?.CurrencyCode ?? CurrencyCatalog.Default.Code).MinorUnitDigits;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_Expense_LoadError", ex.Message);
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
            _errors["Amount"] = L["Finance_Common_AmountPositiveError"];

        if (_form.BankAccountId == Guid.Empty)
            _errors["BankAccountId"] = L["Finance_Expense_BankAccountRequiredError"];

        if (_form.ExpenseAccountId == Guid.Empty)
            _errors["ExpenseAccountId"] = L["Finance_Expense_ExpenseAccountRequiredError"];

        if (_errors.Count > 0)
            return;

        _saving = true;
        try
        {
            var request = new RecordExpenseRequest
            {
                Date = _form.Date,
                Amount = _form.Amount,
                BankAccountId = _form.BankAccountId,
                ExpenseAccountId = _form.ExpenseAccountId,
                TaxCode = _isTaxApplicable ? _form.TaxCode : null,
                Payee = string.IsNullOrWhiteSpace(_form.Payee) ? null : _form.Payee.Trim(),
                Description = string.IsNullOrWhiteSpace(_form.Description) ? null : _form.Description.Trim()
            };

            await ExpensePaymentService.RecordExpenseAsync(request);
            _successMessage = Loc.Get<FinanceResource>("Finance_Expense_SuccessMessage",
                MoneyFormatter.Format(request.Amount));
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_Expense_RecordError", ex.Message);
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
        _form.Payee = null;
        _form.Description = null;
        _form.Date = DateTime.Today;
    }
}
