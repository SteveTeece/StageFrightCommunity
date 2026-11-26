using Microsoft.Extensions.Localization;
using StageFright.Core.Modules.Dashboard;
using StageFright.Core.Modules.Events;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.Core.Modules.Members;
using StageFright.Core.Modules.Organisations;
using StageFright.Core.Modules.Rehearsals;
using StageFright.Core.Modules.Settings;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Organisations;

/// <summary>
/// Unit tests for OrganisationsMenuItemProvider (FR-001): one top-level item routing to the
/// register, placed directly after Members. Reports (DisplayOrder 6) lives in StageFright.UI and
/// is covered by <c>ReportMenuItemProviderTests</c> in StageFright.UI.Tests.
/// </summary>
public class OrganisationsMenuItemProviderTests
{
    private readonly IStringLocalizer<NavigationResource> _localizer = RealStringLocalizer.For<NavigationResource>();
    private readonly OrganisationsMenuItemProvider _sut;

    public OrganisationsMenuItemProviderTests()
    {
        _sut = new OrganisationsMenuItemProvider(_localizer);
    }

    [Fact]
    public void Should_ReturnSingleOrganisationsItem_When_GettingMenuItems()
    {
        // Compared against the resolved keys (not literals): the test host's UI culture picks the
        // spelling ("Organisations" en-AU vs "Organizations" en-US).
        var title = _localizer["Nav_Organisations_Title"];
        var shortLabel = _localizer["Nav_Organisations_ShortLabel"];
        Assert.False(title.ResourceNotFound);
        Assert.False(shortLabel.ResourceNotFound);

        var item = Assert.Single(_sut.GetMenuItems());

        Assert.Equal(title.Value, item.Title);
        Assert.Equal("/organisations", item.Route);
        Assert.Equal(shortLabel.Value, item.ShortLabel);
        Assert.Equal(0, item.DisplayOrder);
        Assert.True(item.SubItems is null || item.SubItems.Count == 0);
    }

    [Fact]
    public void Should_ExposeOrganisationsModuleAtOrderTwo_When_Constructed()
    {
        Assert.Equal("Organisations", _sut.ModuleName);
        Assert.Equal(2, _sut.DisplayOrder);
    }

    [Fact]
    public void Should_OrderCoreProvidersAroundOrganisations_When_ComparingDisplayOrders()
    {
        var localizer = RealStringLocalizer.For<NavigationResource>();

        Assert.Equal(0, new DashboardMenuItemProvider(localizer).DisplayOrder);
        Assert.Equal(1, new MemberMenuItemProvider(localizer).DisplayOrder);
        Assert.Equal(2, new OrganisationsMenuItemProvider(localizer).DisplayOrder);
        Assert.Equal(3, new RehearsalMenuItemProvider(localizer).DisplayOrder);
        Assert.Equal(4, new EventsMenuItemProvider(localizer).DisplayOrder);
        Assert.Equal(5, new FinanceMenuItemProvider(localizer).DisplayOrder);
        Assert.Equal(999, new SettingsMenuItemProvider(localizer).DisplayOrder);
    }
}
