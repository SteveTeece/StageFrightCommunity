using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Resources.Strings;
using CoreValidationException = StageFright.Core.Exceptions.ValidationException;

namespace StageFright.UI.Pages.Finance;

public partial class ReconciliationWorkspace : ComponentBase
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private IBankReconciliationService ReconciliationService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private bool _loading = true;
    private bool _busy;
    private string? _errorMessage;

    private ReconciliationWorkspaceView? _workspace;

    private bool IsFinalised => _workspace?.Reconciliation.Status == ReconciliationStatus.Finalised;

    private bool IsBalanced => _workspace is not null && Math.Abs(_workspace.Difference) <= 0.005m;

    /// <summary>"to {date}" heading suffix.</summary>
    private string ToDateText() =>
        Loc.Get<FinanceResource>("Finance_Reconciliation_ToDate",
            _workspace?.Reconciliation.StatementDate.ToString("d MMM yyyy") ?? string.Empty);

    protected override async Task OnParametersSetAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _errorMessage = null;
        try
        {
            _workspace = await ReconciliationService.GetWorkspaceAsync(Id);
        }
        catch (EntityNotFoundException)
        {
            _workspace = null;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_Reconciliation_WorkspaceLoadError", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ToggleAsync(Guid transactionId)
    {
        _busy = true;
        _errorMessage = null;

        try
        {
            await ReconciliationService.ToggleClearAsync(Id, transactionId);
            await LoadAsync();
        }
        catch (Exception ex) when (ex is CoreValidationException or ReconciliationException)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_Reconciliation_ToggleError", ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task FinaliseAsync()
    {
        _busy = true;
        _errorMessage = null;

        try
        {
            await ReconciliationService.FinaliseAsync(Id);
            await LoadAsync();
        }
        catch (ReconciliationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_Reconciliation_FinaliseError", ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task DeleteDraftAsync()
    {
        _busy = true;
        _errorMessage = null;

        try
        {
            await ReconciliationService.DeleteDraftAsync(Id);
            Navigation.NavigateTo("/finance/reconciliation");
        }
        catch (ReconciliationException ex)
        {
            _errorMessage = ex.Message;
            _busy = false;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_Reconciliation_DeleteError", ex.Message);
            _busy = false;
        }
    }
}
