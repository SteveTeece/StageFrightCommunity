using Microsoft.AspNetCore.Components;

namespace StageFright.UI.Pages.Finance;

public partial class FinancePage : ComponentBase
{
    [SupplyParameterFromQuery(Name = "tab")]
    private string? TabQuery { get; set; }

    private string ActiveTab { get; set; } = "balances";

    [Inject] private NavigationManager Nav { get; set; } = null!;

    protected override void OnInitialized()
    {
        var validTabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "balances", "payments", "annual-fees" };

        if (!string.IsNullOrEmpty(TabQuery) && validTabs.Contains(TabQuery))
            ActiveTab = TabQuery.ToLowerInvariant();
    }

    private void ActivateTab(string tab)
    {
        ActiveTab = tab;
        Nav.NavigateTo($"/finance?tab={tab}", replace: true);
    }
}
