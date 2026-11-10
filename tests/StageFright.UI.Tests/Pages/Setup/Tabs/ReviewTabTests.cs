using Bunit;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
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
    public void NeverRenders_LoadSampleDataRow()
    {
        // The sample-data choice is no longer surfaced on Review at all (spec 029 FR-014) —
        // it moved to the first-run screen, which never displays a review-style summary.
        var cut = Render<ReviewTab>(p => p.Add(x => x.Model, new SetupFormModel()));

        Assert.DoesNotContain("Load sample data", cut.Markup);
        Assert.Empty(cut.FindAll("#seedData"));
    }

    [Fact]
    public void Should_GroupOrganisationBeforeAccounting_When_Rendered()
    {
        var model = new SetupFormModel { OrganizationName = "My Choir", AnnualFee = 80m };
        var cut = Render<ReviewTab>(p => p.Add(x => x.Model, model));

        var organisationHeading = cut.Markup.IndexOf("review-group-organisation", StringComparison.Ordinal);
        var orgName = cut.Markup.IndexOf("My Choir", StringComparison.Ordinal);
        var accountingHeading = cut.Markup.IndexOf("review-group-accounting", StringComparison.Ordinal);
        var annualFee = cut.Markup.IndexOf(MoneyFormatter.Format(80m), StringComparison.Ordinal);

        Assert.True(organisationHeading >= 0);
        Assert.True(orgName > organisationHeading);
        Assert.True(accountingHeading > orgName);
        Assert.True(annualFee > accountingHeading);
    }

    [Fact]
    public void Should_ShowCurrencyFinancialYearAndInception_When_Rendered()
    {
        var model = new SetupFormModel
        {
            CurrencyCode = "NZD",
            FinancialYearStartMonth = 4,
            FinancialYearStartDay = 1,
            InceptionDate = new DateTime(1998, 3, 14)
        };
        var cut = Render<ReviewTab>(p => p.Add(x => x.Model, model));

        Assert.Contains("(NZD)", cut.Markup);
        Assert.Contains(new DateTime(2000, 4, 1).ToString("M"), cut.Markup);
        Assert.Contains(new DateTime(1998, 3, 14).ToString("d"), cut.Markup);
    }

    [Fact]
    public void Should_ShowNotSet_When_InceptionDateBlank()
    {
        var cut = Render<ReviewTab>(p => p.Add(x => x.Model, new SetupFormModel()));

        Assert.Contains("Not set", cut.Markup);
    }

    [Fact]
    public void Should_ShowTaxEntryMode_When_TaxApplicable()
    {
        var model = new SetupFormModel { IsTaxApplicable = true, TaxRate = 10m, TaxEntryMode = TaxEntryMode.Exclusive };
        var cut = Render<ReviewTab>(p => p.Add(x => x.Model, model));

        Assert.Contains(TaxEntryMode.Exclusive.LocalizeEnum(), cut.Markup);
    }

    [Fact]
    public void Should_HideTaxEntryMode_When_TaxNotApplicable()
    {
        var model = new SetupFormModel { TaxEntryMode = TaxEntryMode.Exclusive };
        var cut = Render<ReviewTab>(p => p.Add(x => x.Model, model));

        Assert.DoesNotContain(TaxEntryMode.Exclusive.LocalizeEnum(), cut.Markup);
    }
}
