using Microsoft.AspNetCore.Components;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>Membership &amp; Fees tab (US1) — fees, renewal month, audit retention,
/// relocated unchanged from the old single-page wizard's Step 2.</summary>
public partial class MembershipFeesTab : ComponentBase
{
    [Parameter, EditorRequired] public SetupFormModel Model { get; set; } = null!;
}
