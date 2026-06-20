using Microsoft.AspNetCore.Components;

namespace StageFright.UI.Pages.Finance;

public partial class FinancePage : ComponentBase
{
    [SupplyParameterFromQuery(Name = "tab")]
    private string? TabQuery { get; set; }

    private int DefaultTabIndex { get; set; }

    [Inject] private NavigationManager Nav { get; set; } = null!;

    protected override void OnInitialized()
    {
        DefaultTabIndex = TabQuery?.ToLowerInvariant() switch
        {
            "payments" => 1,
            "annual-fees" => 2,
            _ => 0
        };
    }

    private void NavToTab(string key) =>
        Nav.NavigateTo($"/finance?tab={key}", replace: true);
}
