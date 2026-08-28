using Bunit;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup.Tabs;

/// <summary>bUnit tests for ReviewTab (US1/US3) — a dl-based read-only summary of every
/// setting, two read-only BorderedListBox summaries (queued committee titles, queued
/// Chart of Accounts entries — FR-006), plus the relocated debug-only seed-data
/// checkbox.</summary>
public class ReviewTabTests : LocalizedTestContext
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
    public void QueuedCommitteeTitles_RenderAsBorderedList_NotPlainText()
    {
        var cut = Render<ReviewTab>(p => p
            .Add(x => x.Model, new SetupFormModel())
            .Add(x => x.QueuedCommitteeTitles, new[] { "Publicity Officer", "Webmaster" }));

        Assert.Contains("Publicity Officer", cut.Markup);
        Assert.Contains("Webmaster", cut.Markup);
        Assert.Equal(2, cut.FindAll(".bordered-list-box-row").Count(r => r.TextContent.Contains("Officer") || r.TextContent.Contains("Webmaster")));
    }

    [Fact]
    public void EmptyQueuedCommitteeTitles_ShowsNone()
    {
        var cut = Render<ReviewTab>(p => p.Add(x => x.Model, new SetupFormModel()));

        Assert.Contains("None.", cut.Markup);
    }

    [Fact]
    public void QueuedAccounts_RenderAsBorderedList()
    {
        var queued = new[]
        {
            new QueuedAccountRequest(Guid.NewGuid(), "Petty Cash", AccountType.Asset, true),
            new QueuedAccountRequest(Guid.NewGuid(), "Grant Income", AccountType.Income, false)
        };
        var cut = Render<ReviewTab>(p => p
            .Add(x => x.Model, new SetupFormModel())
            .Add(x => x.QueuedAccounts, queued));

        Assert.Contains("Petty Cash", cut.Markup);
        Assert.Contains("Bank/Cash", cut.Markup);
        Assert.Contains("Grant Income", cut.Markup);
    }

    [Fact]
    public void EmptyQueuedAccounts_ShowsNoneQueued()
    {
        var cut = Render<ReviewTab>(p => p.Add(x => x.Model, new SetupFormModel()));

        Assert.Contains("None queued.", cut.Markup);
    }

    [Fact]
    public void QueuedListSummaries_RenderReadOnly_NoRemoveButtons()
    {
        var queued = new[] { new QueuedAccountRequest(Guid.NewGuid(), "Petty Cash", AccountType.Asset, false) };
        var cut = Render<ReviewTab>(p => p
            .Add(x => x.Model, new SetupFormModel())
            .Add(x => x.QueuedCommitteeTitles, new[] { "Publicity Officer" })
            .Add(x => x.QueuedAccounts, queued));

        Assert.Empty(cut.FindAll(".bordered-list-box-remove"));
    }

    [Fact]
    public void SeedDataChoice_RendersReadOnlyRow_WhenDebugSeederAvailable()
    {
        var cut = Render<ReviewTab>(p => p
            .Add(x => x.Model, new SetupFormModel())
            .Add(x => x.DebugSeederAvailable, true)
            .Add(x => x.SeedWithTestData, true));

        Assert.Contains("Load sample data", cut.Markup);
        Assert.Contains("Yes", cut.Markup);
    }

    [Fact]
    public void SeedDataChoice_RendersNothing_WhenDebugSeederUnavailable()
    {
        var cut = Render<ReviewTab>(p => p
            .Add(x => x.Model, new SetupFormModel())
            .Add(x => x.DebugSeederAvailable, false));

        Assert.DoesNotContain("Load sample data", cut.Markup);
    }

    [Fact]
    public void SeedDataChoice_IsReadOnly_NoCheckboxRendered()
    {
        var cut = Render<ReviewTab>(p => p
            .Add(x => x.Model, new SetupFormModel())
            .Add(x => x.DebugSeederAvailable, true));

        Assert.Empty(cut.FindAll("#seedData"));
    }
}
