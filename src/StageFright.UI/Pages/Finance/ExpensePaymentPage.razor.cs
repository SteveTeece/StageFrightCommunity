using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;

namespace StageFright.UI.Pages.Finance;

public partial class ExpensePaymentPage : ComponentBase
{
    [Inject] private IExpensePaymentService ExpensePaymentService { get; set; } = null!;
    [Inject] private IAccountService AccountService { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;

    private readonly ExpensePaymentModel _form = new();
    private readonly Dictionary<string, string> _errors = new();
    private IReadOnlyList<Account> _expenseAccounts = [];
    private IReadOnlyList<Account> _bankAccounts = [];
    private bool _loading = true;
    private bool _saving;
    private bool _isGstRegistered;
    private string? _successMessage;
    private string? _errorMessage;

    private string? GstInclusiveHint =>
        _isGstRegistered && _form.GstCode == GstCode.Gst && _form.Amount > 0m
            ? $"Includes GST of {GstCalculator.SplitInclusive(_form.Amount).Gst:C}"
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
            _isGstRegistered = settings?.IsGstRegistered ?? false;
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
            _errors["Amount"] = "Amount must be greater than zero.";

        if (_form.BankAccountId == Guid.Empty)
            _errors["BankAccountId"] = "Please select the account the expense was paid from.";

        if (_form.ExpenseAccountId == Guid.Empty)
            _errors["ExpenseAccountId"] = "Please select an expense account.";

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
                GstCode = _isGstRegistered ? _form.GstCode : null,
                Payee = string.IsNullOrWhiteSpace(_form.Payee) ? null : _form.Payee.Trim(),
                Description = string.IsNullOrWhiteSpace(_form.Description) ? null : _form.Description.Trim()
            };

            await ExpensePaymentService.RecordExpenseAsync(request);
            _successMessage = $"Expense of {request.Amount:C} recorded successfully.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to record expense: {ex.Message}";
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
        _form.GstCode = null;
        _form.Payee = null;
        _form.Description = null;
        _form.Date = DateTime.Today;
    }
}
