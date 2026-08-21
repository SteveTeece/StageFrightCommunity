using Bunit;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup.Tabs;

/// <summary>bUnit tests for ReviewTab's US1 (basic) shape — a dl-based read-only summary
/// of every setting, plus the relocated debug-only seed-data checkbox. US3/T045 later
/// upgrades the committee-titles/queued-accounts lines to BorderedListBox summaries.</summary>
public class ReviewTabTests : BunitContext
{
    [Fact]
    public void SummarisesOrganisationNameAndFees()
    {
        var model = new SetupFormModel { OrganizationName = "My Choir", AnnualFee = 80m, AttendanceFee = 6m };
        var cut = Render<ReviewTab>(p => p.Add(x => x.Model, model));

        Assert.Contains("My Choir", cut.Markup);
        Assert.Contains(80m.ToString("C"), cut.Markup);
        Assert.Contains(6m.ToString("C"), cut.Markup);
    }

    [Fact]
    public void SeedDataCheckbox_OnlyShown_WhenDebugSeederAvailable()
    {
        var cutHidden = Render<ReviewTab>(p => p
            .Add(x => x.Model, new SetupFormModel())
            .Add(x => x.DebugSeederAvailable, false));
        Assert.Empty(cutHidden.FindAll("#seedData"));

        var cutShown = Render<ReviewTab>(p => p
            .Add(x => x.Model, new SetupFormModel())
            .Add(x => x.DebugSeederAvailable, true));
        cutShown.Find("#seedData");
    }

    [Fact]
    public async Task CheckingSeedData_InvokesSeedWithTestDataChanged()
    {
        var changedTo = default(bool?);
        var cut = Render<ReviewTab>(p => p
            .Add(x => x.Model, new SetupFormModel())
            .Add(x => x.DebugSeederAvailable, true)
            .Add(x => x.SeedWithTestDataChanged, v => changedTo = v));

        await cut.Find("#seedData").ChangeAsync(true);

        Assert.True(changedTo);
    }
}
