using Microsoft.Extensions.Localization;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Contributes the Finance navigation group. The Overview page keeps its tabs
/// (Outstanding / Record Income / Record Expense / Apply Annual Fees — recording a member
/// payment happens inline on the Outstanding tab); the expanded accounting surfaces are
/// sub-items.
/// DisplayOrder=4 places Finance after Events (3) and before Reports (5).
/// </summary>
public class FinanceMenuItemProvider : IMenuItemProvider
{
    private readonly IStringLocalizer<NavigationResource> _localizer;

    public FinanceMenuItemProvider(IStringLocalizer<NavigationResource> localizer)
    {
        _localizer = localizer;
    }

    public string ModuleName => "Finance";
    public int DisplayOrder => 4;

    public IReadOnlyList<MenuItem> GetMenuItems() =>
    [
        new MenuItem
        {
            Title = _localizer["Nav_Finance_Title"],
            Route = "/finance",
            ShortLabel = _localizer["Nav_Finance_ShortLabel"],
            DisplayOrder = 0,
            SubItems =
            [
                new MenuItem { Title = _localizer["Nav_Finance_Overview"], Route = "/finance", DisplayOrder = 0 },
                new MenuItem { Title = _localizer["Nav_Finance_ChartOfAccounts"], Route = "/finance/accounts", DisplayOrder = 1 },
                new MenuItem { Title = _localizer["Nav_Finance_RecordBankDeposit"], Route = "/finance/bank-deposit", DisplayOrder = 3 },
                new MenuItem { Title = _localizer["Nav_Finance_JournalEntries"], Route = "/finance/journal", DisplayOrder = 4 },
                new MenuItem { Title = _localizer["Nav_Finance_Reconciliation"], Route = "/finance/reconciliation", DisplayOrder = 5 },
                new MenuItem { Title = _localizer["Nav_Finance_OpeningBalances"], Route = "/finance/opening-balances", DisplayOrder = 6 }
            ]
        }
    ];
}
