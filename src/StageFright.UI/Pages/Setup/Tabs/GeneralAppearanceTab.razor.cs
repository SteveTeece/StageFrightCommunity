using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>General section of the Organisation Settings tab — the organisation name only.
/// Currency, financial-year start and inception date moved to <see cref="AccountingTab"/>
/// (spec 031). The theme dropdown lives in <see cref="ThemeSelectionTab"/>, rendered lower in
/// the same tab.</summary>
public partial class GeneralAppearanceTab : ComponentBase
{
    [Parameter, EditorRequired] public SetupFormModel Model { get; set; } = null!;

    [Inject] private IStringLocalizer<SetupResource> L { get; set; } = null!;
}
