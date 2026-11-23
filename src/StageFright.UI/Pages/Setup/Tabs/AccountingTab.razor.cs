using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>Accounting tab (spec 031, US2) — every money-related setup choice in one place:
/// the mandatory currency picker (spec 028, <c>id="setup-currency"</c>), the financial-year
/// start month + day pickers (<c>id="setup-fy-start-month"</c> / <c>id="setup-fy-start-day"</c>),
/// the optional inception date, the annual and attendance fees, and the hosted
/// <see cref="SalesTaxTab"/> below them so each fee sits above its tax treatment (FR-005).
/// SalesTaxTab's column markup assumes a surrounding <c>row</c>, which this tab supplies.</summary>
public partial class AccountingTab : ComponentBase
{
    [Parameter, EditorRequired] public SetupFormModel Model { get; set; } = null!;

    [Inject] private IStringLocalizer<SetupResource> L { get; set; } = null!;
}
