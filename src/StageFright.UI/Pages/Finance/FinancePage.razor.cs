using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.UI.Resources.Strings;

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
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;

    protected override void OnInitialized()
    {
        DefaultTabIndex = TabQuery?.ToLowerInvariant() switch
        {
            "record-income" => 1,
            "record-expense" => 2,
            "annual-fees" => 3,
            _ => 0
        };
    }

    // Router reuses this component instance for same-route query-string navigation (e.g.
    // clicking "Record Member Payment"), so OnInitialized never re-fires — SelectedMemberId
    // must be recomputed here on every parameter update instead.
    protected override void OnParametersSet()
    {
        SelectedMemberId = MemberIdQuery ?? Guid.Empty;
    }

    private void NavToTab(string key) =>
        Nav.NavigateTo($"/finance?tab={key}", replace: true);
}
