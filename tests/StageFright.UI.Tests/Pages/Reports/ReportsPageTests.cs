using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;
using StageFright.Reports.Rendering;
using StageFright.UI.Pages.Reports;

namespace StageFright.UI.Tests.Pages.Reports;

/// <summary>
/// bUnit tests for ReportsPage's report hosting: every report renders through ReportViewer's
/// generic table, except Chart of Accounts, whose on-screen body is the shared
/// ChartOfAccountsGrid also used by Finance → Chart of Accounts (issue #367).
/// </summary>
public class ReportsPageTests : LocalizedTestContext
{
    private readonly IReportProviderRegistry _registry = Substitute.For<IReportProviderRegistry>();
    private readonly IAccountBalanceService _accountBalanceService = Substitute.For<IAccountBalanceService>();
    private readonly IReportProvider _provider = Substitute.For<IReportProvider>();

    private static readonly ReportData FlatReport = new()
    {
        Title = "Some Report",
        GeneratedAt = DateTime.UtcNow,
        Columns = [new ReportColumn { Header = "Name" }],
        Sections = [new ReportSection { Heading = "Section", Rows = [new ReportRow { Cells = ["Generic row"] }] }]
    };

    public ReportsPageTests()
    {
        Services.AddSingleton(_registry);
        Services.AddSingleton(_accountBalanceService);
        Services.AddSingleton(Substitute.For<IPdfReportRenderer>());
        Services.AddSingleton(Substitute.For<ICsvReportExporter>());
        Services.AddSingleton(Substitute.For<ISettingsService>());

        _provider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>()).Returns(FlatReport);
        _accountBalanceService.GetActiveAccountBalancesAsync(Arg.Any<CancellationToken>()).Returns(new List<AccountBalance>
        {
            new() { AccountId = Guid.NewGuid(), AccountNumber = "1100", Name = "Cash on Hand", Type = AccountType.Asset, Balance = 500m },
            new() { AccountId = Guid.NewGuid(), AccountNumber = "4000", Name = "Membership Fees", Type = AccountType.Income, Balance = 250m }
        });
    }

    private IRenderedComponent<ReportsPage> RenderReport(string reportId, IReadOnlyList<ReportFilterDefinition>? filters = null)
    {
        _provider.ReportId.Returns(reportId);
        _provider.Filters.Returns(filters ?? Array.Empty<ReportFilterDefinition>());
        _registry.GetProvider(reportId).Returns(_provider);
        return Render<ReportsPage>(p => p.Add(x => x.ReportId, reportId));
    }

    [Fact]
    public void Should_RenderSharedCoaGrid_When_ChartOfAccountsReportOpened()
    {
        var cut = RenderReport("chart-of-accounts");

        cut.WaitForAssertion(() => Assert.Equal(["Assets", "Income"],
            cut.FindAll(".chart-of-accounts-section").Select(h => h.TextContent.Trim())));
        Assert.DoesNotContain("Generic row", cut.Markup);
    }

    [Fact]
    public async Task Should_ShowBalancesInCoaGrid_When_IncludeBalancesFilterApplied()
    {
        var cut = RenderReport("chart-of-accounts",
        [
            new ReportFilterDefinition { Key = "includeBalances", Type = ReportFilterType.Boolean, Label = "Include balances", DefaultValue = "false" }
        ]);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".chart-of-accounts-section")));
        Assert.DoesNotContain("$500.00", cut.Markup);

        cut.Find("input[type=checkbox]").Change(true);
        await cut.FindAll("button").First(b => b.TextContent.Contains("Apply")).ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => Assert.Contains("$500.00", cut.Markup));
    }

    [Fact]
    public void Should_RenderGenericReportTable_When_OtherReportOpened()
    {
        var cut = RenderReport("income-statement");

        cut.WaitForAssertion(() => Assert.Contains("Generic row", cut.Markup));
        Assert.Empty(cut.FindAll(".chart-of-accounts-section"));
    }
}
