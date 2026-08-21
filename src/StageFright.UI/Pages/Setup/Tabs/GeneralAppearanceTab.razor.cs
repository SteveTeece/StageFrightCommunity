using Microsoft.AspNetCore.Components;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>General tab (US1) — organisation name. The theme dropdown (US6) lives in
/// <see cref="ThemeSelectionTab"/>, rendered lower in the Organisation Settings tab.</summary>
public partial class GeneralAppearanceTab : ComponentBase
{
    [Parameter, EditorRequired] public SetupFormModel Model { get; set; } = null!;
}
