using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Shared;
using CoreValidationException = StageFright.Core.Exceptions.ValidationException;

namespace StageFright.UI.Pages.Finance;

public partial class ChartOfAccountsPage : ComponentBase
{
    [Inject] private IAccountService AccountService { get; set; } = null!;
    [Inject] private IAccountBalanceService AccountBalanceService { get; set; } = null!;

    private bool _loading = true;
    private bool _creating;
    private string? _errorMessage;
    private string? _successMessage;

    private List<AccountBalance> _accounts = new();
    private List<AccountBalance> _archivedAccounts = new();
    private AccountType? _typeFilter;

    private Guid? _editingId;
    private string _editName = string.Empty;

    private IReadOnlyList<string> ExistingAccountNames =>
        _accounts.Select(a => a.Name).Concat(_archivedAccounts.Select(a => a.Name)).ToList();

    private IEnumerable<AccountBalance> FilteredAccounts =>
        _typeFilter is null ? _accounts : _accounts.Where(a => a.Type == _typeFilter);

    protected override async Task OnInitializedAsync()
    {
        await LoadAccountsAsync();
    }

    private async Task LoadAccountsAsync()
    {
        _loading = true;
        _errorMessage = null;
        try
        {
            _accounts = (await AccountBalanceService.GetActiveAccountBalancesAsync()).ToList();
            _archivedAccounts = (await AccountBalanceService.GetArchivedAccountBalancesAsync()).ToList();
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

    private void OnTypeFilterChanged(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        _typeFilter = Enum.TryParse<AccountType>(value, out var parsed) ? parsed : null;
    }

    private async Task HandleCreateAsync(NewAccountModel newAccountModel)
    {
        _creating = true;
        _errorMessage = null;
        _successMessage = null;

        try
        {
            var created = await AccountService.CreateAsync(
                newAccountModel.Name!,
                newAccountModel.Type,
                newAccountModel.Type == AccountType.Asset && newAccountModel.IsBankAccount);
            _successMessage = $"Account '{created.Name}' created with number {created.AccountNumber}.";
            await LoadAccountsAsync();
        }
        catch (CoreValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to create account: {ex.Message}";
        }
        finally
        {
            _creating = false;
        }
    }

    private void StartRename(AccountBalance account)
    {
        _editingId = account.AccountId;
        _editName = account.Name;
        _errorMessage = null;
        _successMessage = null;
    }

    private void CancelRename()
    {
        _editingId = null;
        _editName = string.Empty;
    }

    private async Task SaveRenameAsync(Guid id)
    {
        _errorMessage = null;
        _successMessage = null;
        try
        {
            await AccountService.UpdateAsync(id, _editName);
            _successMessage = "Account renamed.";
            _editingId = null;
            await LoadAccountsAsync();
        }
        catch (CoreValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to rename account: {ex.Message}";
        }
    }

    private async Task ArchiveAsync(Guid id)
    {
        _errorMessage = null;
        _successMessage = null;

        try
        {
            await AccountService.ArchiveAsync(id);
            _successMessage = "Account archived.";
            await LoadAccountsAsync();
        }
        catch (CoreValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to archive account: {ex.Message}";
        }
    }

    private async Task RestoreAsync(Guid id)
    {
        _errorMessage = null;
        _successMessage = null;

        try
        {
            await AccountService.RestoreAsync(id);
            _successMessage = "Account restored.";
            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to restore account: {ex.Message}";
        }
    }

}
