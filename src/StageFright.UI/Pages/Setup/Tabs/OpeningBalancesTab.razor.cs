using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Settings;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>Opening Balances tab (US2) — hosts the shared <see cref="OpeningBalanceEntryForm"/>
/// covering every eligible existing account plus this session's queued accounts (each
/// represented as a placeholder <see cref="Account"/> keyed by its
/// <see cref="QueuedAccountRequest.ClientId"/> until Finish resolves it to a real
/// <c>Account.Id</c> — data-model.md's account-reference resolution). Nothing here posts
/// to the ledger (FR-018); OnSubmit bubbles the entered request up so SetupWizard can
/// queue it, matching <see cref="Tabs.ChartOfAccountsTab"/>'s own queuing pattern.</summary>
public partial class OpeningBalancesTab : ComponentBase
{
    [Inject] private IOpeningBalanceService OpeningBalanceService { get; set; } = null!;

    [Parameter, EditorRequired] public IReadOnlyList<QueuedAccountRequest> QueuedAccounts { get; set; } = Array.Empty<QueuedAccountRequest>();
    [Parameter, EditorRequired] public EventCallback<RecordOpeningBalancesRequest> OnSubmit { get; set; }

    private IReadOnlyList<Account> _existingAccounts = Array.Empty<Account>();

    // Defaults to today (research.md — no FinancialYearStartMonth exists yet during setup,
    // unlike the standalone OpeningBalancesWizard which reads it once Settings exists).
    private DateTime _asAtDate = DateTime.Today;

    /// <summary>Real accounts eligible for an opening balance, plus a placeholder Account
    /// per queued Chart of Accounts entry so it gets its own row before it's ever created
    /// (FR-020: the row list stays in sync as the caller's queued-account set changes,
    /// since OpeningBalanceEntryForm reconciles its rows against Accounts on every
    /// parameter set).</summary>
    private IReadOnlyList<Account> AllAccounts =>
        _existingAccounts.Concat(QueuedAccounts.Select(ToPlaceholderAccount)).ToList();

    protected override async Task OnInitializedAsync()
    {
        _existingAccounts = await OpeningBalanceService.GetOpeningBalanceAccountsAsync();
    }

    private static Account ToPlaceholderAccount(QueuedAccountRequest queued) => new()
    {
        Id = queued.ClientId,
        Name = queued.Name,
        Type = queued.Type,
        IsBankAccount = queued.IsBankAccount,
        AccountNumber = "(new)",
        IsSystem = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private void HandleAsAtDateChanged(ChangeEventArgs e)
    {
        _asAtDate = DateTime.TryParse(e.Value?.ToString(), out var date) ? date : _asAtDate;
    }
}
