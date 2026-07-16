using Microsoft.AspNetCore.Components;

namespace StageFright.UI.Pages.Finance;

public partial class FinancePage : ComponentBase
{
    [SupplyParameterFromQuery(Name = "tab")]
    private string? TabQuery { get; set; }

    [SupplyParameterFromQuery(Name = "memberId")]
    private Guid? MemberIdQuery { get; set; }

    private int DefaultTabIndex { get; set; }
    private Guid SelectedMemberId { get; set; }

    [Inject] private NavigationManager Nav { get; set; } = null!;

    protected override void OnInitialized()
    {
        SelectedMemberId = MemberIdQuery ?? Guid.Empty;
        DefaultTabIndex = TabQuery?.ToLowerInvariant() switch
        {
            "record-income" => 1,
            "record-expense" => 2,
            "annual-fees" => 3,
            _ => 0
        };
    }

    private void NavToTab(string key) =>
        Nav.NavigateTo($"/finance?tab={key}", replace: true);
}
