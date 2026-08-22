using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup.Tabs;

/// <summary>bUnit tests for GeneralAppearanceTab (US1) — the organisation name field.
/// Rendered inside an EditForm since its InputText/ValidationMessage need a cascaded
/// EditContext (built manually — EditForm.ChildContent is generic). The theme dropdown
/// (US6) has its own test coverage in ThemeSelectionTabTests.</summary>
public class GeneralAppearanceTabTests : BunitContext
{
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
}
