using StageFright.Core.Modules.Finance;

namespace StageFright.UI.Shared;

/// <summary>
/// One account row in <see cref="ChartOfAccountsGrid"/>. <see cref="SectionOrder"/> is what the
/// grid groups on, so the type header rows read Assets, Liabilities, Equity, Income, Expenses —
/// the same order as the Chart of Accounts report — rather than <c>AccountType</c>'s enum order.
/// </summary>
public class ChartOfAccountsGridRow
{
    public required AccountBalance Account { get; init; }

    public required int SectionOrder { get; init; }
}
