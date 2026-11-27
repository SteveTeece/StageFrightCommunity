using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Shared;

/// <summary>
/// Read-only view of one bank deposit: its date, description, bank account, status, cash and
/// total, and the received items it banked. Shared by Finance → Banking's deposit detail page
/// (spec 033) and the Bank Deposits report's deposit page (issue #399).
/// </summary>
public partial class DepositDetailView : ComponentBase
{
    [Parameter, EditorRequired] public DepositDetail Detail { get; set; } = null!;

    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private decimal ItemsTotal => Detail.Items.Sum(i => i.Amount);

    private string StatusText()
    {
        if (!Detail.IsReversed)
            return L["Finance_Banking_Status_Recorded"];

        return Detail.ReversedAt is { } reversedAt
            ? $"{L["Finance_Banking_Status_Reversed"]} · {Loc.Get<FinanceResource>("Finance_Banking_ReversedOn", reversedAt.ToLocalTime().ToShortDateString())}"
            : L["Finance_Banking_Status_Reversed"];
    }

    private string PayerText(AwaitingDepositItem item) =>
        item.Source == ReceivedItemSource.OpeningBalance && string.IsNullOrEmpty(item.PayerName)
            ? L["Finance_Banking_OpeningBalancePayer"].Value
            : item.PayerName ?? string.Empty;
}
