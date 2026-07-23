using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Finance;

namespace StageFright.UI.Pages.Finance;

public partial class BankDepositPage : ComponentBase
{
    [Inject] private IBankDepositService BankDepositService { get; set; } = null!;
    [Inject] private IAccountService AccountService { get; set; } = null!;

    private readonly BankDepositModel _form = new();
    private readonly Dictionary<string, string> _errors = new();
    private IReadOnlyList<Account> _destinationAccounts = [];
    private bool _loading = true;
    private bool _saving;
    private string? _successMessage;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var bankAccounts = await AccountService.GetBankAccountsAsync();
            _destinationAccounts = bankAccounts.Where(a => a.Id != SystemAccounts.CashId).ToList();
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

        if (_form.ToAccountId == Guid.Empty)
            _errors["ToAccountId"] = "Please select the destination account.";

        if (_errors.Count > 0)
            return;

        _saving = true;
        try
        {
            var request = new RecordBankDepositRequest
            {
                Date = _form.Date,
                Amount = _form.Amount,
                ToAccountId = _form.ToAccountId,
                Description = string.IsNullOrWhiteSpace(_form.Description) ? null : _form.Description.Trim()
            };

            await BankDepositService.RecordDepositAsync(request);
            _successMessage = $"Deposit of {request.Amount:C} recorded successfully.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to record deposit: {ex.Message}";
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
        _form.Description = null;
        _form.Date = DateTime.Today;
    }
}
