using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Shared;

/// <summary>
/// The one Chart of Accounts grid (issue #367), shared by Finance → Chart of Accounts and the
/// on-screen Reports → Chart of Accounts view: a header row per account type in the report's
/// order (Assets, Liabilities, Equity, Income, Expenses) with that type's accounts indented
/// beneath it in account-number order, and a Balance column shown only when
/// <see cref="IncludeBalances"/> is on. <see cref="RowActions"/> (Finance only) adds a per-row
/// actions column; <see cref="EditTemplate"/> replaces the name cell of the account being renamed.
/// </summary>
public partial class ChartOfAccountsGrid : ComponentBase
{
    private static readonly AccountType[] SectionTypes =
        [AccountType.Asset, AccountType.Liability, AccountType.Equity, AccountType.Income, AccountType.Expense];

    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;

    [Parameter, EditorRequired] public IEnumerable<AccountBalance> Accounts { get; set; } = [];
    [Parameter] public bool IncludeBalances { get; set; }
    [Parameter] public RenderFragment<AccountBalance>? RowActions { get; set; }
    [Parameter] public string ActionsTitle { get; set; } = string.Empty;
    [Parameter] public Guid? EditingAccountId { get; set; }
    [Parameter] public RenderFragment<AccountBalance>? EditTemplate { get; set; }

    private IReadOnlyList<ChartOfAccountsGridRow> Rows => Accounts
        .Select(a => new ChartOfAccountsGridRow { Account = a, SectionOrder = Array.IndexOf(SectionTypes, a.Type) })
        .OrderBy(r => r.SectionOrder)
        .ThenBy(r => r.Account.AccountNumber, StringComparer.Ordinal)
        .ToList();

    private void OnRender(DataGridRenderEventArgs<ChartOfAccountsGridRow> args)
    {
        if (!args.FirstRender)
            return;

        args.Grid.Groups.Add(new GroupDescriptor
        {
            Property = nameof(ChartOfAccountsGridRow.SectionOrder),
            SortOrder = SortOrder.Ascending
        });
        StateHasChanged();
    }

    private string SectionHeading(Group group) => SectionTypes[Convert.ToInt32(group.Data.Key)] switch
    {
        AccountType.Asset => L["Finance_Coa_FilterAssets"],
        AccountType.Liability => L["Finance_Coa_FilterLiabilities"],
        AccountType.Equity => L["Finance_Coa_FilterEquity"],
        AccountType.Income => L["Finance_Coa_FilterIncome"],
        _ => L["Finance_Coa_FilterExpenses"]
    };
}
