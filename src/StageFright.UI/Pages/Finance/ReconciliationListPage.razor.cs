using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Resources.Strings;
using StageFright.UI.Shared;
using CoreValidationException = StageFright.Core.Exceptions.ValidationException;

namespace StageFright.UI.Pages.Finance;

public partial class ReconciliationListPage : ComponentBase
{
    [Inject] private IAccountService AccountService { get; set; } = null!;
    [Inject] private IBankReconciliationService ReconciliationService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;
    [Inject] private ILogger<ReconciliationListPage> Logger { get; set; } = null!;

    private bool _loading = true;
    private bool _starting;
    private string? _errorMessage;

    private List<Account> _bankAccounts = new();
    private Guid? _selectedAccountId;
    private BankReconciliation? _draft;
    private List<BankReconciliation> _history = new();

    private StartModel _startModel = new();

    /// <summary>"A draft reconciliation to {date} is in progress." banner text.</summary>
    private string DraftInProgressText() =>
        Loc.Get<FinanceResource>("Finance_Reconciliation_DraftInProgress",
            _draft?.StatementDate.ToString("d MMM yyyy") ?? string.Empty);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _bankAccounts = (await AccountService.GetBankAccountsAsync()).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error in {Operation}", "ReconciliationListPage.OnInitializedAsync");
            _errorMessage = Loc.Get<FinanceResource>("Finance_Reconciliation_LoadAccountsError", ErrorDetail.For(ex, Loc));
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task OnAccountChangedAsync(ChangeEventArgs e)
    {
        _selectedAccountId = Guid.TryParse(e.Value?.ToString(), out var id) ? id : null;
        _startModel = new StartModel();
        await LoadAccountAsync();
    }

    private async Task LoadAccountAsync()
    {
        _errorMessage = null;
        _draft = null;
        _history.Clear();

        if (_selectedAccountId is null)
            return;

        try
        {
            _draft = await ReconciliationService.GetDraftForAccountAsync(_selectedAccountId.Value);
            _history = (await ReconciliationService.GetHistoryAsync(_selectedAccountId.Value)).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error in {Operation}", "ReconciliationListPage.LoadAccountAsync");
            _errorMessage = Loc.Get<FinanceResource>("Finance_Reconciliation_LoadError", ErrorDetail.For(ex, Loc));
        }
    }

    private async Task StartDraftAsync()
    {
        if (_selectedAccountId is null)
            return;

        _starting = true;
        _errorMessage = null;

        try
        {
            var draft = await ReconciliationService.StartDraftAsync(new StartReconciliationRequest
            {
                AccountId = _selectedAccountId.Value,
                StatementDate = _startModel.StatementDate,
                StatementClosingBalance = _startModel.StatementClosingBalance,
                Notes = _startModel.Notes
            });
            Navigation.NavigateTo($"/finance/reconciliation/{draft.Id}");
        }
        catch (Exception ex) when (ex is CoreValidationException or ReconciliationException)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error in {Operation}", "ReconciliationListPage.StartDraftAsync");
            _errorMessage = Loc.Get<FinanceResource>("Finance_Reconciliation_StartError", ErrorDetail.For(ex, Loc));
        }
        finally
        {
            _starting = false;
        }
    }

    private void ResumeDraft()
    {
        if (_draft is not null)
            Navigation.NavigateTo($"/finance/reconciliation/{_draft.Id}");
    }

    private void OpenWorkspace(Guid reconciliationId)
    {
        Navigation.NavigateTo($"/finance/reconciliation/{reconciliationId}");
    }

    private sealed class StartModel
    {
        [Required]
        public DateTime StatementDate { get; set; } = DateTime.UtcNow.Date;

        [Required]
        public decimal StatementClosingBalance { get; set; }

        [MaxLength(500, ErrorMessageResourceType = typeof(FormValidationMessages), ErrorMessageResourceName = nameof(FormValidationMessages.Finance_Validation_ReconciliationNotesTooLong))]
        public string? Notes { get; set; }
    }
}
