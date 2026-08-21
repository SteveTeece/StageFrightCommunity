using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.UI.Layout;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup.Tabs;

/// <summary>bUnit tests for GeneralAppearanceTab (US1/US6) — organisation name field and
/// the theme dropdown. Rendered inside an EditForm since its InputText/ValidationMessage
/// need a cascaded EditContext (built manually — EditForm.ChildContent is generic).</summary>
public class GeneralAppearanceTabTests : BunitContext
{
    public GeneralAppearanceTabTests()
    {
        Services.AddSingleton(Substitute.For<ISettingsService>());
        Services.AddSingleton(Substitute.For<IDeviceThemePreferenceProvider>());
    }

    private static RenderFragment<EditContext> GeneralTab(SetupFormModel model) => _ => builder =>
    {
        builder.OpenComponent<GeneralAppearanceTab>(0);
        builder.AddComponentParameter(1, nameof(GeneralAppearanceTab.Model), model);
        builder.CloseComponent();
    };

    [Fact]
    public void RendersOrganisationNameField_BoundToModel()
    {
        var model = new SetupFormModel();
        var cut = Render<EditForm>(p => p
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, GeneralTab(model)));

        cut.Find("#orgName").Change("Springfield Choral Society");

        Assert.Equal("Springfield Choral Society", model.OrganizationName);
    }

    [Fact]
    public void RendersThemeDropdown()
    {
        var model = new SetupFormModel();
        var cut = Render<ThemeProvider>(p => p.AddChildContent<EditForm>(f => f
            .Add(x => x.Model, model)
            .Add(x => x.ChildContent, GeneralTab(model))));

        cut.Find("#themeSelect");
    }
}
