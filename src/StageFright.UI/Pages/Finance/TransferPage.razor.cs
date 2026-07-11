using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Finance;

namespace StageFright.UI.Pages.Finance;

public partial class TransferPage : ComponentBase
{
    [Inject] private IAccountTransferService TransferService { get; set; } = null!;
    [Inject] private IAccountService AccountService { get; set; } = null!;

    private readonly TransferModel _form = new();
    private readonly Dictionary<string, string> _errors = new();
    private IReadOnlyList<Account> _bankAccounts = [];
    private bool _loading = true;
    private bool _saving;
    private string? _successMessage;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _bankAccounts = await AccountService.GetBankAccountsAsync();
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

        if (_form.FromAccountId == Guid.Empty)
            _errors["FromAccountId"] = "Please select the source account.";

        if (_form.ToAccountId == Guid.Empty)
            _errors["ToAccountId"] = "Please select the destination account.";

        if (_form.FromAccountId != Guid.Empty && _form.FromAccountId == _form.ToAccountId)
            _errors["ToAccountId"] = "The source and destination accounts must differ.";

        if (_errors.Count > 0)
            return;

        _saving = true;
        try
        {
            var request = new RecordTransferRequest
            {
                Date = _form.Date,
                Amount = _form.Amount,
                FromAccountId = _form.FromAccountId,
                ToAccountId = _form.ToAccountId,
                Description = string.IsNullOrWhiteSpace(_form.Description) ? null : _form.Description.Trim()
            };

            await TransferService.RecordTransferAsync(request);
            _successMessage = $"Transfer of {request.Amount:C} recorded successfully.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to record transfer: {ex.Message}";
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
