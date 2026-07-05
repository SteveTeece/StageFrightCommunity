using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Contributes the Finance navigation group. The Overview page keeps its tabs
/// (Balances / Record Member Payment / Record Income / Apply Annual Fees);
/// the expanded accounting surfaces are sub-items.
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
                new MenuItem { Title = "Chart of Accounts", Route = "/finance/accounts", DisplayOrder = 1 }
            ]
        }
    ];
}
