using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StageFright.Core.Localization;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup;

/// <summary>
/// bUnit coverage for the first-run currency picker (spec 028, US1 / FR-001): it lives on
/// <see cref="GeneralAppearanceTab"/> as <c>#setup-currency</c>, lists every
/// <see cref="CurrencyCatalog"/> entry, defaults to <c>AUD</c>, and two-way binds
/// <see cref="SetupFormModel.CurrencyCode"/>.
/// </summary>
public class CurrencyPickerTests : LocalizedTestContext
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
    public void RendersOneOptionPerCatalogEntry()
    {
        var cut = Render(new SetupFormModel());

        var options = cut.Find("#setup-currency").QuerySelectorAll("option");

        Assert.Equal(CurrencyCatalog.All.Count, options.Length);
        Assert.All(CurrencyCatalog.All, c =>
            Assert.Contains(options, o => o.GetAttribute("value") == c.Code));
    }

    [Fact]
    public void DefaultsToAud()
    {
        var model = new SetupFormModel();

        var cut = Render(model);

        Assert.Equal("AUD", model.CurrencyCode);
        Assert.Equal("AUD", cut.Find("#setup-currency").GetAttribute("value"));
    }

    [Fact]
    public void BindsSelectionToModel()
    {
        var model = new SetupFormModel();
        var cut = Render(model);

        cut.Find("#setup-currency").Change("JPY");

        Assert.Equal("JPY", model.CurrencyCode);
    }
}
