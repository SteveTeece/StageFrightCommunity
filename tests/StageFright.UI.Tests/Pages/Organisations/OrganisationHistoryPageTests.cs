using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Organisations;
using StageFright.UI.Pages.Organisations;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Tests.Pages.Organisations;

/// <summary>
/// bUnit tests for OrganisationHistoryPage, the History tab (spec 032, US3): the grid's date,
/// description, account and amount columns with "Total paid" / "Total received" (FR-011), amounts
/// through MoneyFormatter under a non-AUD currency (FR-013), an archived organisation's history
/// still rendering, the empty state, the not-found state and a failed load.
/// </summary>
public class OrganisationHistoryPageTests : LocalizedTestContext
{
    private readonly IExternalOrganisationService _organisations = Substitute.For<IExternalOrganisationService>();
    private readonly IOrganisationHistoryService _history = Substitute.For<IOrganisationHistoryService>();
    private readonly Guid _id = Guid.NewGuid();

    private static readonly OrganisationHistoryEntry Received = new(
        Guid.NewGuid(), new DateTime(2026, 4, 1), "Town Hall grant", "Donations", JournalEntryType.Income, 200m);

    private static readonly OrganisationHistoryEntry Paid = new(
        Guid.NewGuid(), new DateTime(2026, 3, 1), "Town Hall — Hall hire", "Hall Hire", JournalEntryType.ExpensePayment, 500m);

    public OrganisationHistoryPageTests()
    {
        Services.AddSingleton(_organisations);
        Services.AddSingleton(_history);
        _organisations.GetByIdAsync(_id, Arg.Any<CancellationToken>()).Returns(Organisation(archived: false));
        _history.GetHistoryAsync(_id, Arg.Any<CancellationToken>())
            .Returns(new OrganisationHistory([Received, Paid], TotalPaid: 500m, TotalReceived: 200m));
    }

    private IStringLocalizer<OrganisationsResource> L =>
        Services.GetRequiredService<IStringLocalizer<OrganisationsResource>>();

    private ExternalOrganisation Organisation(bool archived) => new()
    {
        Id = _id, Name = "Town Hall", IsDeleted = archived,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private IRenderedComponent<OrganisationHistoryPage> RenderHistory() =>
        Render<OrganisationHistoryPage>(p => p.Add(c => c.Id, _id));

    [Fact]
    public void Should_ShowDateDescriptionAccountAndAmountWithTotals_When_OrganisationHasHistory()
    {
        var cut = RenderHistory();

        var headers = cut.FindAll("th").Select(th => th.TextContent.Trim()).ToList();
        foreach (var key in new[] { "ColDate", "ColDescription", "ColAccount", "ColAmount" })
            Assert.Contains(L[$"Organisations_History_{key}"].Value, headers);

        var rows = cut.FindAll("tbody tr");
        Assert.Equal(2, rows.Count);
        Assert.Contains("Town Hall grant", rows[0].TextContent);
        Assert.Contains("Donations", rows[0].TextContent);
        Assert.Contains("1 Apr 2026", rows[0].TextContent);
        Assert.Contains(MoneyFormatter.Format(200m), rows[0].TextContent);
        Assert.Contains("Hall Hire", rows[1].TextContent);

        Assert.Contains(L["Organisations_History_TotalPaid"].Value, cut.Markup);
        Assert.Contains(L["Organisations_History_TotalReceived"].Value, cut.Markup);
        Assert.Equal(MoneyFormatter.Format(500m), cut.Find("#totalPaid").TextContent.Trim());
        Assert.Equal(MoneyFormatter.Format(200m), cut.Find("#totalReceived").TextContent.Trim());
    }

    [Fact]
    public void Should_MarkHistoryTabActive_When_Rendered()
    {
        var cut = RenderHistory();

        Assert.Contains("active", cut.Find("#organisationTabHistory").ClassList);
    }

    [Fact]
    public void Should_FormatAmountsWithConfiguredCurrency_When_CurrencyIsNotAud()
    {
        MoneyFormatter.Configure(CurrencyCatalog.Get("JPY"));
        try
        {
            var cut = RenderHistory();

            var expectedPaid = MoneyFormatter.Format(500m);
            Assert.Contains("¥", expectedPaid);
            Assert.Equal(expectedPaid, cut.Find("#totalPaid").TextContent.Trim());
            Assert.Equal(MoneyFormatter.Format(200m), cut.Find("#totalReceived").TextContent.Trim());
            Assert.Contains(expectedPaid, cut.FindAll("tbody tr")[1].TextContent);
            Assert.DoesNotContain("$", cut.Find("#totalPaid").TextContent);
        }
        finally
        {
            MoneyFormatter.Configure(CurrencyCatalog.Default);
        }
    }

    [Fact]
    public void Should_StillRenderHistoryWithArchivedBadge_When_OrganisationIsArchived()
    {
        _organisations.GetByIdAsync(_id, Arg.Any<CancellationToken>()).Returns(Organisation(archived: true));

        var cut = RenderHistory();

        Assert.Contains(L["Organisations_Detail_ArchivedBadge"].Value, cut.Markup);
        Assert.Equal(2, cut.FindAll("tbody tr").Count);
        Assert.Equal(MoneyFormatter.Format(500m), cut.Find("#totalPaid").TextContent.Trim());
    }

    [Fact]
    public void Should_ShowEmptyStateAndZeroTotals_When_OrganisationHasNoHistory()
    {
        _history.GetHistoryAsync(_id, Arg.Any<CancellationToken>())
            .Returns(new OrganisationHistory([], 0m, 0m));

        var cut = RenderHistory();

        Assert.Contains(L["Organisations_History_Empty"].Value, cut.Markup);
        Assert.Empty(cut.FindAll("tbody tr"));
        Assert.Equal(MoneyFormatter.Format(0m), cut.Find("#totalPaid").TextContent.Trim());
    }

    [Fact]
    public void Should_ShowNotFoundAndSkipHistory_When_OrganisationUnknown()
    {
        _organisations.GetByIdAsync(_id, Arg.Any<CancellationToken>()).Returns((ExternalOrganisation?)null);

        var cut = RenderHistory();

        Assert.Contains(L["Organisations_Detail_NotFound"].Value, cut.Markup);
        _history.DidNotReceiveWithAnyArgs().GetHistoryAsync(default, Xunit.TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Should_ShowUnexpectedError_When_HistoryLoadFails()
    {
        _history.GetHistoryAsync(_id, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        var cut = RenderHistory();

        Assert.Equal(L["Organisations_Error_Unexpected"].Value, cut.Find("#organisationHistoryError").TextContent.Trim());
        Assert.Empty(cut.FindAll("#totalPaid"));
    }
}
