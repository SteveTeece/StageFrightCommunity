using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Contributes the Finance navigation group. The Overview page keeps its tabs
/// (Outstanding / Record Member Payment / Record Income / Record Expense / Apply Annual
/// Fees); the expanded accounting surfaces are sub-items.
/// DisplayOrder=4 places Finance after Events (3) and before Reports (5).
/// </summary>
public class FinanceMenuItemProvider : IMenuItemProvider
{
    public string ModuleName => "Finance";
    public int DisplayOrder => 4;

    public IReadOnlyList<MenuItem> GetMenuItems() =>
    [
        new MenuItem
        {
            Title = "Finance",
            Route = "/finance",
            ShortLabel = "FIN",
            DisplayOrder = 0,
            SubItems =
            [
                new MenuItem { Title = "Overview", Route = "/finance", DisplayOrder = 0 },
                new MenuItem { Title = "Chart of Accounts", Route = "/finance/accounts", DisplayOrder = 1 },
                new MenuItem { Title = "Transfers", Route = "/finance/transfers", DisplayOrder = 3 },
                new MenuItem { Title = "Journal Entries", Route = "/finance/journal", DisplayOrder = 4 },
                new MenuItem { Title = "Reconciliation", Route = "/finance/reconciliation", DisplayOrder = 5 },
                new MenuItem { Title = "Opening Balances", Route = "/finance/opening-balances", DisplayOrder = 6 }
            ]
        }
    ];
}
