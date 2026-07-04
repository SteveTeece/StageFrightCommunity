using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Contributes the top-level Finance navigation item. Balances, payments, and
/// annual fees are reached via tabs on the Finance page itself.
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
            DisplayOrder = 0
        }
    ];
}
