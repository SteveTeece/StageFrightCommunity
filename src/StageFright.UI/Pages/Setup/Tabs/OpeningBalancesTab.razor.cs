using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Resources.Strings;
using StageFright.UI.Shared;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>Opening Balances tab (US2) — hosts the shared <see cref="OpeningBalanceEntryForm"/>
/// covering every eligible existing account plus this session's queued accounts (each
/// represented as a placeholder <see cref="Account"/> keyed by its
/// <see cref="QueuedAccountRequest.ClientId"/> until Finish resolves it to a real
/// <c>Account.Id</c> — data-model.md's account-reference resolution). Nothing here posts
/// to the ledger (FR-018). There is no "Queue" button (issue #382): OnChange reports the
/// full current entry set to SetupWizard every time an amount or the as-at date changes,
/// so whatever is on screen is what Next and Finish carry forward.</summary>
public partial class OpeningBalancesTab : ComponentBase
{
    [Inject] private IOpeningBalanceService OpeningBalanceService { get; set; } = null!;
    [Inject] private IStringLocalizer<SetupResource> L { get; set; } = null!;

    [Parameter, EditorRequired] public IReadOnlyList<QueuedAccountRequest> QueuedAccounts { get; set; } = Array.Empty<QueuedAccountRequest>();
    [Parameter, EditorRequired] public EventCallback<RecordOpeningBalancesRequest> OnChange { get; set; }

    private IReadOnlyList<Account> _existingAccounts = Array.Empty<Account>();

    // The last entry set the form reported, kept so a date-only change can re-report it.
    private List<OpeningBalanceEntry> _entries = new();

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

    private Task HandleEntriesChangedAsync(RecordOpeningBalancesRequest request)
    {
        _entries = request.Entries;
        return OnChange.InvokeAsync(request);
    }

    private Task HandleAsAtDateChangedAsync(ChangeEventArgs e)
    {
        _asAtDate = DateTime.TryParse(e.Value?.ToString(), out var date) ? date : _asAtDate;

        // Drop entries for a queued account removed since the last amount change — the
        // form has already dropped its row (FR-020), so re-reporting it would resurrect it.
        var accountIds = AllAccounts.Select(a => a.Id).ToHashSet();
        return OnChange.InvokeAsync(new RecordOpeningBalancesRequest
        {
            AsAtDate = _asAtDate,
            Entries = _entries.Where(entry => accountIds.Contains(entry.AccountId)).ToList()
        });
    }
}
