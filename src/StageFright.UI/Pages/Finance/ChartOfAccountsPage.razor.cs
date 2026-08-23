using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Rendering;
using StageFright.UI.Shared;
using CoreValidationException = StageFright.Core.Exceptions.ValidationException;

namespace StageFright.UI.Pages.Finance;

public partial class ChartOfAccountsPage : ComponentBase
{
    [Inject] private IAccountService AccountService { get; set; } = null!;
    [Inject] private IAccountBalanceService AccountBalanceService { get; set; } = null!;
    [Inject] private IReportProviderRegistry ReportProviderRegistry { get; set; } = null!;
    [Inject] private IPdfReportRenderer PdfRenderer { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private ILogger<ChartOfAccountsPage> Logger { get; set; } = null!;

    private bool _loading = true;
    private string? _errorMessage;
    private string? _successMessage;

    private List<AccountBalance> _accounts = new();
    private List<AccountBalance> _archivedAccounts = new();
    private AccountType? _typeFilter;
    private bool _includeBalances;

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

    private async Task PrintAsync()
    {
        _errorMessage = null;
        _successMessage = null;

        try
        {
            var provider = ReportProviderRegistry.GetProvider("chart-of-accounts");
            if (provider == null)
            {
                _errorMessage = "Unable to print. Please try again.";
                return;
            }

            var filters = new ReportFilterValues();
            filters.Set("includeBalances", _includeBalances ? "true" : "false");
            var report = await provider.GenerateAsync(filters);

            var settings = await SettingsService.GetAsync();
            var orgName = settings?.OrganizationName ?? string.Empty;
            var bytes = PdfRenderer.Render(report, orgName);
            var tempPath = Path.Combine(Path.GetTempPath(), $"chart-of-accounts_{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(tempPath, bytes);
#pragma warning disable CA1416
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath) { UseShellExecute = true });
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to print chart of accounts");
            _errorMessage = "Unable to print. Please try again.";
        }
    }
}
