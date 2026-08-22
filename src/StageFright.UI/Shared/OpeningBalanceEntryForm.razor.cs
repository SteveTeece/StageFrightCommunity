using System.Globalization;
using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Finance;

namespace StageFright.UI.Shared;

/// <summary>
/// Shared opening-balance entry experience (FR-017/FR-019) used both by the standalone
/// Opening Balances page (immediate-post <see cref="OnSubmit"/>) and the setup wizard's
/// Opening Balances tab (deferred-queue <see cref="OnSubmit"/>) — folds the standalone
/// page's former Step 2 (enter) and Step 3 (confirm/post) into one entry table + submit
/// action, per research.md's Dependency Inversion decision. <see cref="Accounts"/> is
/// reconciled against the row list on every parameter set, preserving already-entered
/// amounts for accounts still present (FR-020: stays in sync as the caller's account set
/// changes — e.g. the wizard's Chart of Accounts tab adding/removing a queued account).
/// </summary>
public partial class OpeningBalanceEntryForm : ComponentBase
{
    [Parameter, EditorRequired] public IReadOnlyList<Account> Accounts { get; set; } = Array.Empty<Account>();
    [Parameter, EditorRequired] public EventCallback<RecordOpeningBalancesRequest> OnSubmit { get; set; }
    [Parameter, EditorRequired] public DateTime AsAtDate { get; set; }
    [Parameter] public bool ShowAlreadyPostedWarning { get; set; } = true;
    [Parameter] public bool HasExistingOpeningBalances { get; set; }
    [Parameter] public string SubmitButtonText { get; set; } = "Post Opening Balances";

    [Inject] private IOpeningBalanceService OpeningBalanceService { get; set; } = null!;

    private List<OpeningBalanceRowModel> _rows = new();
    private bool _submitting;

    private IReadOnlyList<OpeningBalanceRowModel> NonZeroRows =>
        _rows.Where(r => r.Amount != 0m).ToList();

    private decimal Plug => OpeningBalanceService.ComputePlug(
        _rows.Select(r => new OpeningBalanceEntry { AccountId = r.Account.Id, Amount = r.Amount }).ToList(),
        Accounts);

    protected override void OnParametersSet()
    {
        var existingByAccountId = _rows.ToDictionary(r => r.Account.Id);
        var reconciled = new List<OpeningBalanceRowModel>(Accounts.Count);
        foreach (var account in Accounts)
        {
            reconciled.Add(existingByAccountId.TryGetValue(account.Id, out var existingRow)
                ? existingRow
                : new OpeningBalanceRowModel { Account = account });
        }
        _rows = reconciled;
    }

    private void SetAmount(OpeningBalanceRowModel row, ChangeEventArgs args)
    {
        row.Amount = decimal.TryParse(
            args.Value?.ToString(), NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
            ? amount
            : 0m;
    }

    private async Task HandleSubmitAsync()
    {
        _submitting = true;
        try
        {
            var request = new RecordOpeningBalancesRequest
            {
                AsAtDate = AsAtDate,
                Entries = NonZeroRows
                    .Select(r => new OpeningBalanceEntry { AccountId = r.Account.Id, Amount = r.Amount })
                    .ToList()
            };

            await OnSubmit.InvokeAsync(request);
        }
        finally
        {
            _submitting = false;
        }
    }
}
