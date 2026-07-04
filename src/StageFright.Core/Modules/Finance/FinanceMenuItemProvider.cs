using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Contributes the Finance navigation section with sub-items for Balances, Payments, and Apply Annual Fees.
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
                new MenuItem { Title = "Balances", Route = "/finance?tab=balances", DisplayOrder = 0 },
                new MenuItem { Title = "Record Member Payment", Route = "/finance?tab=record-payment", DisplayOrder = 1 },
                new MenuItem { Title = "Record Income", Route = "/finance?tab=record-income", DisplayOrder = 2 },
                new MenuItem { Title = "Apply Annual Fees", Route = "/finance/annual-fees", DisplayOrder = 3 }
            ]
        }
    ];
}
