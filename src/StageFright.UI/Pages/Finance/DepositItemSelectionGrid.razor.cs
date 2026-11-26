using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Finance;

/// <summary>Tickable list of the cheques and other items awaiting deposit (spec 033, FR-009).</summary>
public partial class DepositItemSelectionGrid
{
    [Parameter] public IReadOnlyList<AwaitingDepositItem> Items { get; set; } = [];
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Raised on every tick with the total of the ticked items.</summary>
    [Parameter] public EventCallback<decimal> SelectionChanged { get; set; }

    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private List<DepositItemRow> _rows = [];

    protected override void OnParametersSet()
    {
        // Rebuild only when the item set changes, or a parent re-render would wipe the ticks.
        if (_rows.Select(r => r.Item.Id).SequenceEqual(Items.Select(i => i.Id)))
            return;

        _rows = Items.Select(i => new DepositItemRow { Item = i }).ToList();
    }

    private bool AllSelected => _rows.Count > 0 && _rows.All(r => r.Selected);

    // The opening-balance item has no payer name, so it shows a translated label instead.
    private string PayerText(AwaitingDepositItem item) =>
        item.Source == ReceivedItemSource.OpeningBalance && string.IsNullOrEmpty(item.PayerName)
            ? L["Finance_Banking_OpeningBalancePayer"].Value
            : item.PayerName ?? string.Empty;

    private string SelectRowAriaLabel(AwaitingDepositItem item) =>
        Loc.Get<FinanceResource>("Finance_Banking_SelectRowAriaLabel", PayerText(item), item.ReceivedDate.ToShortDateString());

    private async Task ToggleSelectAll(bool value)
    {
        foreach (var row in _rows)
            row.Selected = value;

        await RaiseSelectionChangedAsync();
    }

    private async Task ToggleRow(DepositItemRow row, bool value)
    {
        row.Selected = value;
        await RaiseSelectionChangedAsync();
    }

    private Task RaiseSelectionChangedAsync() =>
        SelectionChanged.InvokeAsync(_rows.Where(r => r.Selected).Sum(r => r.Item.Amount));

    /// <summary>The ids of the ticked items.</summary>
    public IReadOnlyList<Guid> GetSelectedItemIds() =>
        _rows.Where(r => r.Selected).Select(r => r.Item.Id).ToList();

    private sealed class DepositItemRow
    {
        public AwaitingDepositItem Item { get; init; } = null!;
        public bool Selected { get; set; }
    }
}
