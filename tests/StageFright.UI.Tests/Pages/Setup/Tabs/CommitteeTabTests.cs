using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup.Tabs;

/// <summary>bUnit tests for CommitteeTab's US1 (legacy) shape — AGM month, seat count
/// target, and the comma-separated titles textbox, relocated unchanged from the old
/// wizard's Step 4. US5/T041 replaces the textbox with a +/− widget. Rendered inside an
/// EditForm since its Input* components need a cascaded EditContext (built manually —
/// EditForm.ChildContent is generic).</summary>
public class CommitteeTabTests : BunitContext
{
    private IRenderedComponent<EditForm> RenderInForm(SetupFormModel model) =>
        Render<EditForm>(p => p
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, (RenderFragment<EditContext>)(_ => builder =>
            {
                builder.OpenComponent<CommitteeTab>(0);
                builder.AddComponentParameter(1, nameof(CommitteeTab.Model), model);
                builder.CloseComponent();
            })));

    [Fact]
    public void RendersAllFields_BoundToModel()
    {
        var model = new SetupFormModel();
        var cut = RenderInForm(model);

        cut.Find("#agmMonth").Change("10");
        cut.Find("#officeHolderTitles").Change("Publicity Officer, Webmaster");
        cut.Find("#seatCountTarget").Change("8");

        Assert.Equal(10, model.CommitteeRenewalMonth);
        Assert.Equal("Publicity Officer, Webmaster", model.CommitteeOfficeHolderTitlesText);
        Assert.Equal(8, model.GeneralCommitteeSeatCountTarget);
    }

    [Fact]
    public void AgmMonthOptions_CoverAllTwelveMonths()
    {
        var cut = RenderInForm(new SetupFormModel());

        Assert.Equal(12, cut.Find("#agmMonth").Children.Length);
    }
}
