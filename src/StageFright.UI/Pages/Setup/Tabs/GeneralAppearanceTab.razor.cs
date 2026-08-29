using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>General tab — organisation name, the mandatory currency picker (spec 028 US1 /
/// FR-001; <c>id="setup-currency"</c>, options from <c>CurrencyCatalog.All</c>), and the
/// mandatory financial-year-start month + day pickers (spec 028 US7 / FR-019, FR-020;
/// <c>id="setup-fy-start-month"</c> / <c>id="setup-fy-start-day"</c>, defaulting to 1 July).
/// The theme dropdown (US6) lives in <see cref="ThemeSelectionTab"/>, rendered lower in the
/// Organisation Settings tab.</summary>
public partial class GeneralAppearanceTab : ComponentBase
{
    [Parameter, EditorRequired] public SetupFormModel Model { get; set; } = null!;

    [Inject] private IStringLocalizer<SetupResource> L { get; set; } = null!;
}
