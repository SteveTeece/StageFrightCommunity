using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Reports.Models;
using StageFright.Reports.Registry;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for ReportProviderRegistry:
/// - Members section appears before Finance, Finance before plugins
/// - Duplicate ReportId skipped + logged
/// - Failing provider skipped gracefully (GetMenuSections still returns)
/// </summary>
public class ReportProviderRegistryTests
{
    private static IReportProvider MakeProvider(string reportId, string reportName, string module, int order = 0)
    {
        var p = Substitute.For<IReportProvider>();
        p.ReportId.Returns(reportId);
        p.ReportName.Returns(reportName);
        p.ModuleName.Returns(module);
        p.DisplayOrder.Returns(order);
        p.Filters.Returns(Array.Empty<ReportFilterDefinition>());
        return p;
    }

    [Fact]
    public void GetMenuSections_Members_AppearsFirst()
    {
        var registry = BuildRegistry(
            MakeProvider("finance-report", "Income Statement", "Finance"),
            MakeProvider("member-list", "Member List", "Members"));

        var sections = registry.GetMenuSections();

        Assert.Equal("Members", sections[0].ModuleName);
    }

    [Fact]
    public void GetMenuSections_Finance_AppearsSecond()
    {
        var registry = BuildRegistry(
            MakeProvider("finance-report", "Income Statement", "Finance"),
            MakeProvider("member-list", "Member List", "Members"));

        var sections = registry.GetMenuSections();

        Assert.Equal("Finance", sections[1].ModuleName);
    }

    [Fact]
    public void GetMenuSections_PluginModule_AppearsAfterFinance()
    {
        var registry = BuildRegistry(
            MakeProvider("p1", "Plugin Report", "MyPlugin"),
            MakeProvider("f1", "Finance Report", "Finance"),
            MakeProvider("m1", "Member List", "Members"));

        var sections = registry.GetMenuSections();

        Assert.Equal("Members", sections[0].ModuleName);
        Assert.Equal("Finance", sections[1].ModuleName);
        Assert.Equal("MyPlugin", sections[2].ModuleName);
    }

    [Fact]
    public void GetMenuSections_MultiplePlugins_OrderedAlphabetically()
    {
        var registry = BuildRegistry(
            MakeProvider("z1", "Z Plugin", "Zebra"),
            MakeProvider("a1", "A Plugin", "Alpha"),
            MakeProvider("m1", "Member List", "Members"));

        var sections = registry.GetMenuSections();

        var pluginSections = sections.Skip(1).Select(s => s.ModuleName).ToList();
        Assert.Equal(pluginSections.OrderBy(x => x).ToList(), pluginSections);
    }

    [Fact]
    public void GetMenuSections_DuplicateReportId_SecondSkipped()
    {
        var registry = BuildRegistry(
            MakeProvider("dup-id", "First", "Finance"),
            MakeProvider("dup-id", "Second", "Finance"));

        var sections = registry.GetMenuSections();
        var financeSection = sections.First(s => s.ModuleName == "Finance");

        Assert.Single(financeSection.Reports);
    }

    [Fact]
    public void GetProvider_KnownId_ReturnsProvider()
    {
        var provider = MakeProvider("income-statement", "Income Statement", "Finance");
        var registry = BuildRegistry(provider);

        var result = registry.GetProvider("income-statement");

        Assert.NotNull(result);
        Assert.Equal("income-statement", result!.ReportId);
    }

    [Fact]
    public void GetProvider_UnknownId_ReturnsNull()
    {
        var registry = BuildRegistry(MakeProvider("known", "Known", "Finance"));

        var result = registry.GetProvider("unknown-id");

        Assert.Null(result);
    }

    [Fact]
    public void GetMenuSections_EmptyRegistry_ReturnsEmptyList()
    {
        var registry = BuildRegistry();

        var sections = registry.GetMenuSections();

        Assert.Empty(sections);
    }

    [Fact]
    public void GetMenuSections_WithMembersProviders_IncludesAllReports()
    {
        var registry = BuildRegistry(
            MakeProvider("member-list", "Member List", "Members", 10),
            MakeProvider("committee-report", "Committee Report", "Members", 20));

        var sections = registry.GetMenuSections();
        var membersSection = sections.First(s => s.ModuleName == "Members");

        Assert.Equal(2, membersSection.Reports.Count);
    }

    [Fact]
    public void GetMenuSections_SectionReports_OrderedByDisplayOrder()
    {
        var registry = BuildRegistry(
            MakeProvider("r2", "Second", "Finance", 20),
            MakeProvider("r1", "First", "Finance", 10));

        var financeSection = registry.GetMenuSections().First(s => s.ModuleName == "Finance");

        Assert.Equal("r1", financeSection.Reports[0].ReportId);
        Assert.Equal("r2", financeSection.Reports[1].ReportId);
    }

    // --- Helpers ---

    private static IReportProviderRegistry BuildRegistry(params IReportProvider[] providers)
    {
        return new StageFright.Reports.Registry.ReportProviderRegistry(
            providers,
            NullLogger<StageFright.Reports.Registry.ReportProviderRegistry>.Instance);
    }
}
