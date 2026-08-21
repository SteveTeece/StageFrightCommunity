using Microsoft.AspNetCore.Components;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>Committee tab (US1) — AGM month, seat count target, and the existing
/// comma-separated office-holder titles field, relocated unchanged from the old
/// single-page wizard's Step 4. US5 later replaces the textbox with a +/− widget and
/// a BorderedListBox.</summary>
public partial class CommitteeTab : ComponentBase
{
    [Parameter, EditorRequired] public SetupFormModel Model { get; set; } = null!;
}
