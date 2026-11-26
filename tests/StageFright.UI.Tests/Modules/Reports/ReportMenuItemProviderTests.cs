using NSubstitute;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.Reports.Registry;
using StageFright.UI.Modules.Reports;

namespace StageFright.UI.Tests.Modules.Reports;

/// <summary>
/// Unit tests for ReportMenuItemProvider's place in the nav bar: spec 032 (FR-001) inserted
/// Organisations at DisplayOrder 2, shifting Reports from 5 to 6 (after Finance, before Settings).
/// </summary>
public class ReportMenuItemProviderTests
{
    private readonly ReportMenuItemProvider _sut;

    public ReportMenuItemProviderTests()
    {
        var registry = Substitute.For<IReportProviderRegistry>();
        registry.GetMenuSections().Returns([]);
        _sut = new ReportMenuItemProvider(registry, RealStringLocalizer.For<NavigationResource>());
    }

    [Fact]
    public void Should_SitAtOrderSix_When_OrganisationsPrecedesIt()
    {
        Assert.Equal("Reports", _sut.ModuleName);
        Assert.Equal(6, _sut.DisplayOrder);
    }

    [Fact]
    public void Should_RouteToReports_When_GettingMenuItems()
    {
        var item = Assert.Single(_sut.GetMenuItems());

        Assert.Equal("/reports", item.Route);
        Assert.Equal(6, item.DisplayOrder);
    }
}
