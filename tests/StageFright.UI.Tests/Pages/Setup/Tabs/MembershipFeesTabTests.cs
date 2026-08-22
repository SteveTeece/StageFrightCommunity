using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup.Tabs;

/// <summary>bUnit tests for MembershipFeesTab (US1) — fees, renewal month, audit
/// retention, relocated unchanged from the old wizard's Step 2. Rendered inside an
/// EditForm since its Input*/ValidationMessage components need a cascaded EditContext.
/// EditForm.ChildContent is RenderFragment&lt;EditContext&gt; (generic), so bUnit's
/// AddChildContent&lt;T&gt; shorthand doesn't apply — built manually instead.</summary>
public class MembershipFeesTabTests : BunitContext
{
    private IRenderedComponent<EditForm> RenderInForm(SetupFormModel model) =>
        Render<EditForm>(p => p
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, (RenderFragment<EditContext>)(_ => builder =>
            {
                builder.OpenComponent<MembershipFeesTab>(0);
                builder.AddComponentParameter(1, nameof(MembershipFeesTab.Model), model);
                builder.CloseComponent();
            })));

    [Fact]
    public void RendersAllFields_BoundToModel()
    {
        var model = new SetupFormModel();
        var cut = RenderInForm(model);

        cut.Find("#annualFee").Change("80");
        cut.Find("#attendanceFee").Change("6");
        cut.Find("#renewalMonth").Change("3");
        cut.Find("#auditRetentionYears").Change("5");

        Assert.Equal(80m, model.AnnualFee);
        Assert.Equal(6m, model.AttendanceFee);
        Assert.Equal(3, model.MembershipRenewalMonth);
        Assert.Equal(5, model.AuditRetentionYears);
    }

    [Fact]
    public void RenewalMonthOptions_CoverAllTwelveMonths()
    {
        var cut = RenderInForm(new SetupFormModel());

        Assert.Equal(12, cut.Find("#renewalMonth").Children.Length);
    }
}
