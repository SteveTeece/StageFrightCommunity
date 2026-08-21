using Microsoft.AspNetCore.Components;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>Sales Tax tab (US1) — tax applicability, rate, per-fee treatment, relocated
/// unchanged from the old single-page wizard's Step 3, including the existing
/// clear-tax-fields-on-toggle-off behavior (Edge Cases).</summary>
public partial class SalesTaxTab : ComponentBase
{
    [Parameter, EditorRequired] public SetupFormModel Model { get; set; } = null!;

    private void HandleTaxToggleChanged()
    {
        if (!Model.IsTaxApplicable)
        {
            Model.TaxRate = null;
            Model.AnnualFeeTaxCode = null;
            Model.AttendanceFeeTaxCode = null;
        }
    }
}
