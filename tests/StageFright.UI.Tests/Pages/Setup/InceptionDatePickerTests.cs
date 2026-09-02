using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup;

/// <summary>
/// bUnit coverage for the first-run optional organisation inception-date picker
/// (spec 028, FR-022 / issue #353): <c>#setup-inception-date</c> lives on
/// <see cref="GeneralAppearanceTab"/>, is always rendered, is <em>optional</em> (no required
/// marker), defaults to blank, and two-way binds <see cref="SetupFormModel.InceptionDate"/>.
/// </summary>
public class InceptionDatePickerTests : LocalizedTestContext
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
    public void RendersADateInput()
    {
        var cut = Render(new SetupFormModel());

        var input = cut.Find("#setup-inception-date");
        Assert.Equal("date", input.GetAttribute("type"));
    }

    [Fact]
    public void DefaultsToBlank()
    {
        var model = new SetupFormModel();

        var cut = Render(model);

        Assert.Null(model.InceptionDate);
        Assert.True(string.IsNullOrEmpty(cut.Find("#setup-inception-date").GetAttribute("value")));
    }

    [Fact]
    public void IsOptional_NoRequiredMarker()
    {
        var cut = Render(new SetupFormModel());

        var group = cut.Find("#setup-inception-date").Closest(".mb-3")!;

        Assert.Empty(group.QuerySelectorAll("span.text-danger"));
    }

    [Fact]
    public void BindsSelectedDateToModel()
    {
        var model = new SetupFormModel();
        var cut = Render(model);

        cut.Find("#setup-inception-date").Change("2026-10-01");

        Assert.Equal(new DateTime(2026, 10, 1), model.InceptionDate);
    }
}
