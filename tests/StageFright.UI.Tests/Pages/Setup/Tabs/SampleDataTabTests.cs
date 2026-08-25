using Bunit;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup.Tabs;

/// <summary>bUnit tests for SampleDataTab (US1) — the debug-only "Load sample data"
/// checkbox, relocated here from ReviewTab so it renders on the Organisation Settings
/// tab instead (FR-001). Markup and behavior are unchanged from the original checkbox;
/// only its host component moved.</summary>
public class SampleDataTabTests : BunitContext
{
    [Fact]
    public void SeedDataCheckbox_OnlyShown_WhenDebugSeederAvailable()
    {
        var cutHidden = Render<SampleDataTab>(p => p.Add(x => x.DebugSeederAvailable, false));
        Assert.Empty(cutHidden.FindAll("#seedData"));

        var cutShown = Render<SampleDataTab>(p => p.Add(x => x.DebugSeederAvailable, true));
        cutShown.Find("#seedData");
    }

    [Fact]
    public async Task CheckingSeedData_InvokesSeedWithTestDataChanged()
    {
        var changedTo = default(bool?);
        var cut = Render<SampleDataTab>(p => p
            .Add(x => x.DebugSeederAvailable, true)
            .Add(x => x.SeedWithTestDataChanged, v => changedTo = v));

        await cut.Find("#seedData").ChangeAsync(true);

        Assert.True(changedTo);
    }

    [Fact]
    public void SeedDataCheckbox_ReflectsCurrentValue()
    {
        var cut = Render<SampleDataTab>(p => p
            .Add(x => x.DebugSeederAvailable, true)
            .Add(x => x.SeedWithTestData, true));

        Assert.True(cut.Find("#seedData").HasAttribute("checked"));
    }
}
