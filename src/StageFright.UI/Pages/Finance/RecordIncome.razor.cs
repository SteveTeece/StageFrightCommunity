using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;

namespace StageFright.UI.Pages.Finance;

public partial class RecordIncome : ComponentBase
{
    [Inject] private IIncomeEntryService IncomeEntryService { get; set; } = null!;
    [Inject] private IAccountService AccountService { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private readonly RecordIncomeModel _form = new();
    private readonly Dictionary<string, string> _errors = new();
    private IReadOnlyList<Account> _accounts = [];
    private IReadOnlyList<Account> _bankAccounts = [];
    private bool _loading = true;
    private bool _saving;
    private bool _isTaxApplicable;
    private decimal _taxRate;
    private string? _successMessage;
    private string? _errorMessage;

    private string? TaxInclusiveHint =>
        _isTaxApplicable && _form.TaxCode == TaxCode.Taxable && _form.Amount > 0m
            ? $"Includes tax of {TaxCalculator.SplitInclusive(_form.Amount, _taxRate).Tax:C}"
            : null;

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

    private async Task SaveAsync()
    {
        _errors.Clear();
        _errorMessage = null;

        if (_form.Amount <= 0m)
        {
            _errors["Amount"] = "Amount must be greater than zero.";
            return;
        }

        if (_form.AccountId == Guid.Empty)
        {
            _errors["AccountId"] = "Please select a account.";
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
            _successMessage = $"Income of {request.Amount:C} recorded successfully.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to record income: {ex.Message}";
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
