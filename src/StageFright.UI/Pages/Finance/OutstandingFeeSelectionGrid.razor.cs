using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Finance;

public partial class OutstandingFeeSelectionGrid
{
    [Parameter] public IReadOnlyList<OutstandingFee> Fees { get; set; } = [];
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public EventCallback<decimal> SelectionChanged { get; set; }

    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private List<OutstandingFeeRow> _rows = [];

    protected override void OnParametersSet()
    {
        // Rebuild only when the underlying fee set actually changes — Blazor re-invokes
        // OnParametersSet on every parent render (e.g. after SelectionChanged fires), and
        // rebuilding unconditionally would wipe out the checkboxes' Selected state.
        if (_rows.Count == Fees.Count && _rows.Select(r => r.Fee.FeeId).SequenceEqual(Fees.Select(f => f.FeeId)))
            return;

        _rows = Fees.Select(f => new OutstandingFeeRow { Fee = f }).ToList();
    }

    private bool AllSelected => _rows.Count > 0 && _rows.All(r => r.Selected);

    /// <summary>aria-label for a fee row's select checkbox — localised fee type + date.</summary>
    private string SelectRowAriaLabel(OutstandingFee fee) =>
        Loc.Get<FinanceResource>("Finance_FeeSelection_SelectRowAriaLabel",
            fee.FeeType.LocalizeEnum(), fee.FeeDate.ToString("d"));

    private async Task ToggleSelectAll(bool value)
    {
        foreach (var row in _rows)
            row.Selected = value;

        await RaiseSelectionChangedAsync();
    }

    private async Task ToggleRow(OutstandingFeeRow row, bool value)
    {
        row.Selected = value;
        await RaiseSelectionChangedAsync();
    }

    private Task RaiseSelectionChangedAsync()
    {
        var sum = _rows.Where(r => r.Selected).Sum(r => r.Fee.RemainingAmount);
        return SelectionChanged.InvokeAsync(sum);
    }

    public IReadOnlyList<Guid> GetSelectedFeeIds()
        => _rows.Where(r => r.Selected).Select(r => r.Fee.FeeId).ToList();

    private sealed class OutstandingFeeRow
    {
        public OutstandingFee Fee { get; init; } = null!;
        public bool Selected { get; set; }
    }
}
