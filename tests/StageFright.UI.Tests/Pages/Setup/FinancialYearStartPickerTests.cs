using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup;

/// <summary>
/// bUnit coverage for the first-run financial-year-start pickers (spec 028, US7 / FR-019, FR-020):
/// month (<c>#setup-fy-start-month</c>) and day (<c>#setup-fy-start-day</c>) live on
/// <see cref="GeneralAppearanceTab"/>, are always rendered and mandatory, default to 1 July, and
/// two-way bind <see cref="SetupFormModel.FinancialYearStartMonth"/> /
/// <see cref="SetupFormModel.FinancialYearStartDay"/>.
/// </summary>
public class FinancialYearStartPickerTests : LocalizedTestContext
{
    private static RenderFragment<EditContext> GeneralTab(SetupFormModel model) => _ => builder =>
    {
        builder.OpenComponent<GeneralAppearanceTab>(0);
        builder.AddComponentParameter(1, nameof(GeneralAppearanceTab.Model), model);
        builder.CloseComponent();
    };

    private IRenderedComponent<EditForm> Render(SetupFormModel model) =>
        Render<EditForm>(p => p
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, GeneralTab(model)));

    [Fact]
    public void RendersTwelveMonthOptionsAndTwentyEightDayOptions()
    {
        var cut = Render(new SetupFormModel());

        Assert.Equal(12, cut.Find("#setup-fy-start-month").QuerySelectorAll("option").Length);
        Assert.Equal(28, cut.Find("#setup-fy-start-day").QuerySelectorAll("option").Length);
    }

    [Fact]
    public void DefaultsToJulyDayOne()
    {
        var model = new SetupFormModel();

        var cut = Render(model);

        Assert.Equal(7, model.FinancialYearStartMonth);
        Assert.Equal(1, model.FinancialYearStartDay);
        Assert.Equal("7", cut.Find("#setup-fy-start-month").GetAttribute("value"));
        Assert.Equal("1", cut.Find("#setup-fy-start-day").GetAttribute("value"));
    }

    [Fact]
    public void BindsMonthAndDaySelectionsToModel()
    {
        var model = new SetupFormModel();
        var cut = Render(model);

        cut.Find("#setup-fy-start-month").Change("4");
        cut.Find("#setup-fy-start-day").Change("6");

        Assert.Equal(4, model.FinancialYearStartMonth);
        Assert.Equal(6, model.FinancialYearStartDay);
    }

    [Fact]
    public void BothPickersAreRenderedAndMarkedRequired()
    {
        var cut = Render(new SetupFormModel());

        var monthGroup = cut.Find("#setup-fy-start-month").Closest(".col-md-6");
        var dayGroup = cut.Find("#setup-fy-start-day").Closest(".col-md-6");

        Assert.NotNull(monthGroup);
        Assert.NotNull(dayGroup);
        Assert.Contains("text-danger", monthGroup!.InnerHtml);
        Assert.Contains("text-danger", dayGroup!.InnerHtml);
    }
}
