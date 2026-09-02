using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StageFright.Core.Enums;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup.Tabs;

/// <summary>bUnit tests for SalesTaxTab (US1) — tax applicability, rate, per-fee
/// treatment, and the clear-on-toggle-off behavior, relocated unchanged from the old
/// wizard's Step 3. Rendered inside an EditForm since its Input* components need a
/// cascaded EditContext (built manually — EditForm.ChildContent is generic).</summary>
public class SalesTaxTabTests : LocalizedTestContext
{
    private IRenderedComponent<EditForm> RenderInForm(SetupFormModel model) =>
        Render<EditForm>(p => p
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, (RenderFragment<EditContext>)(_ => builder =>
            {
                builder.OpenComponent<SalesTaxTab>(0);
                builder.AddComponentParameter(1, nameof(SalesTaxTab.Model), model);
                builder.CloseComponent();
            })));

    [Fact]
    public void RateAndTreatmentFields_OnlyShown_WhenTaxApplicable()
    {
        var model = new SetupFormModel();
        var cut = RenderInForm(model);

        Assert.Empty(cut.FindAll("#taxRate"));

        cut.Find("#taxApplicable").Change(true);

        cut.Find("#taxRate");
        cut.Find("#annualFeeTaxCode");
        cut.Find("#attendanceFeeTaxCode");
    }

    [Fact]
    public void TogglingOff_ClearsRateAndTreatmentFields()
    {
        var model = new SetupFormModel
        {
            IsTaxApplicable = true,
            TaxRate = 10m,
            AnnualFeeTaxCode = TaxCode.Taxable,
            AttendanceFeeTaxCode = TaxCode.Taxable
        };
        var cut = RenderInForm(model);

        cut.Find("#taxApplicable").Change(false);

        Assert.Null(model.TaxRate);
        Assert.Null(model.AnnualFeeTaxCode);
        Assert.Null(model.AttendanceFeeTaxCode);
    }
}
