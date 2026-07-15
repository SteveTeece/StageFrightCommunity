using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Dashboard;
using StageFright.Plugins.Contracts;
using StageFright.UI.Pages.Dashboard;
using StageFright.UI.Shared;

namespace StageFright.UI.Tests.Pages.Dashboard;

/// <summary>
/// bUnit tests for the Dashboard page — section rendering, tile placement by DisplayOrder,
/// loading state, error tile display, and Extensions section visibility.
/// TileRenderer is stubbed to isolate Dashboard structure from async tile loading.
/// </summary>
public class DashboardTests : BunitContext
{
    private readonly IDashboardService _dashboardService = Substitute.For<IDashboardService>();

    public DashboardTests()
    {
        Services.AddSingleton(_dashboardService);
        ComponentFactories.AddStub<TileRenderer>(parameters =>
        {
            var provider = parameters.Get(x => x.Provider);
            return $"<div class=\"tile-stub\" data-tile-id=\"{provider.TileId}\" data-module=\"{provider.ModuleName}\"></div>";
        });
    }

    // --- Core Metrics section ---

    [Fact]
    public void Renders_CoreMetrics_Section_Heading()
    {
        SetupProviders(MakeCoreProvider("members", 10));

        var cut = Render<StageFright.UI.Pages.Dashboard.Dashboard>();

        Assert.Contains("Core Metrics", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Renders_TileCard_ForEachCoreProvider()
    {
        SetupProviders(
            MakeCoreProvider("members", 10),
            MakeCoreProvider("rehearsals", 20),
            MakeCoreProvider("finance", 40));

        var cut = Render<StageFright.UI.Pages.Dashboard.Dashboard>();

        Assert.Equal(3, cut.FindAll("[data-tile-id]").Count);
    }

    [Fact]
    public void CoreTile_HasCorrectTileId_InStub()
    {
        SetupProviders(MakeCoreProvider("members", 10));

        var cut = Render<StageFright.UI.Pages.Dashboard.Dashboard>();

        cut.Find("[data-tile-id='members']");
    }

    // --- Extensions section ---

    [Fact]
    public void ExtensionsSection_NotRendered_WhenNoPluginTiles()
    {
        SetupProviders(MakeCoreProvider("members", 10));

        var cut = Render<StageFright.UI.Pages.Dashboard.Dashboard>();

        Assert.DoesNotContain("Extensions", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtensionsSection_Rendered_WhenPluginTilePresent()
    {
        SetupProviders(
            MakeCoreProvider("members", 10),
            MakePluginProvider("test-tile", 100));

        var cut = Render<StageFright.UI.Pages.Dashboard.Dashboard>();

        Assert.Contains("Extensions", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PluginTile_AppearsInExtensionsSection_NotCoreSection()
    {
        SetupProviders(
            MakeCoreProvider("members", 10),
            MakePluginProvider("test-tile", 100));

        var cut = Render<StageFright.UI.Pages.Dashboard.Dashboard>();

        var extensionsSection = cut.Find("[aria-label='Extensions']");
        Assert.NotNull(extensionsSection.QuerySelector("[data-tile-id='test-tile']"));
    }

    [Fact]
    public void CoreTile_DoesNotAppearInExtensionsSection()
    {
        SetupProviders(
            MakeCoreProvider("members", 10),
            MakePluginProvider("test-tile", 100));

        var cut = Render<StageFright.UI.Pages.Dashboard.Dashboard>();

        var coreSection = cut.Find("[aria-label='Core Metrics']");
        Assert.Empty(coreSection.QuerySelectorAll("[data-tile-id='test-tile']"));
    }

    // --- Loading state ---

    [Fact]
    public void BeforeInitialized_ShowsLoadingIndicator()
    {
        // Use a never-completing task to freeze initialization
        _dashboardService.GetTilesAsync(Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource<IReadOnlyList<IDashboardTileProvider>>().Task);

        var cut = Render<StageFright.UI.Pages.Dashboard.Dashboard>();

        // Tiles section not yet visible; loading state should be shown
        Assert.DoesNotContain("Core Metrics", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // --- Empty state ---

    [Fact]
    public void WhenNoProviders_CoreSection_IsEmpty()
    {
        SetupProviders();

        var cut = Render<StageFright.UI.Pages.Dashboard.Dashboard>();

        var coreSection = cut.Find("[aria-label='Core Metrics']");
        Assert.Empty(coreSection.QuerySelectorAll("[data-tile-id]"));
    }

    // --- Helpers ---

    // --- Header action links (design 3a) ---

    [Fact]
    public void Should_RenderHeaderActionLink_When_ProviderHasActionTextAndRoute()
    {
        SetupProviders(MakeLinkedProvider("members", 10, "/members", "View Members"));

        var cut = Render<StageFright.UI.Pages.Dashboard.Dashboard>();

        var link = cut.Find(".tile-action-link");
        Assert.Equal("View Members", link.TextContent);
        Assert.Equal("/members", link.GetAttribute("href"));
    }

    [Fact]
    public void Should_NotRenderHeaderActionLink_When_ProviderHasNoActionText()
    {
        SetupProviders(MakeCoreProvider("members", 10));

        var cut = Render<StageFright.UI.Pages.Dashboard.Dashboard>();

        Assert.Empty(cut.FindAll(".tile-action-link"));
    }

    [Fact]
    public void Should_ApplyTileIdClassToCard_When_TileRenders()
    {
        SetupProviders(MakeCoreProvider("finance", 40));

        var cut = Render<StageFright.UI.Pages.Dashboard.Dashboard>();

        Assert.NotNull(cut.Find(".card.tile-finance"));
        Assert.NotNull(cut.Find(".card.sf-dash-tile"));
    }

    // --- Tile size classes ---

    [Fact]
    public void Should_ApplyDefaultSizeClass_When_ProviderDoesNotOverrideTileSize()
    {
        SetupProviders(MakeCoreProvider("members", 10));

        var cut = Render<StageFright.UI.Pages.Dashboard.Dashboard>();

        Assert.NotNull(cut.Find(".card.tile-members.tile-size-1x1"));
    }

    [Fact]
    public void Should_ApplyConfiguredSizeClass_When_ProviderOverridesTileSize()
    {
        SetupProviders(MakeSizedProvider("rehearsals-attendance-trend", 60, DashboardTileSize.OneByTwo));

        var cut = Render<StageFright.UI.Pages.Dashboard.Dashboard>();

        Assert.NotNull(cut.Find(".card.tile-rehearsals-attendance-trend.tile-size-1x2"));
    }

    [Fact]
    public void Should_RenderIndependentGridContainers_ForCoreAndExtensionsSections()
    {
        SetupProviders(
            MakeCoreProvider("members", 10),
            MakePluginProvider("test-tile", 100));

        var cut = Render<StageFright.UI.Pages.Dashboard.Dashboard>();

        var coreSection = cut.Find("[aria-label='Core Metrics']");
        var extensionsSection = cut.Find("[aria-label='Extensions']");
        Assert.NotNull(coreSection.QuerySelector(".sf-dash-grid"));
        Assert.NotNull(extensionsSection.QuerySelector(".sf-dash-grid"));
    }

    [Fact]
    public void Should_ApplyDistinctSizeClasses_When_CoreTilesHaveMixedSizes()
    {
        SetupProviders(
            MakeSizedProvider("members", 10, DashboardTileSize.OneByOne),
            MakeSizedProvider("rehearsals-attendance-trend", 60, DashboardTileSize.OneByTwo),
            MakeSizedProvider("finance-cashflow", 50, DashboardTileSize.TwoByOne),
            MakeSizedProvider("finance", 40, DashboardTileSize.TwoByTwo));

        var cut = Render<StageFright.UI.Pages.Dashboard.Dashboard>();

        Assert.NotNull(cut.Find(".card.tile-members.tile-size-1x1"));
        Assert.NotNull(cut.Find(".card.tile-rehearsals-attendance-trend.tile-size-1x2"));
        Assert.NotNull(cut.Find(".card.tile-finance-cashflow.tile-size-2x1"));
        Assert.NotNull(cut.Find(".card.tile-finance.tile-size-2x2"));
    }

    [Fact]
    public void Should_ApplySizeClass_When_PluginTileOverridesTileSize_InExtensionsSection()
    {
        SetupProviders(
            MakeCoreProvider("members", 10),
            MakeSizedProvider("test-tile", 100, DashboardTileSize.OneByTwo));

        var cut = Render<StageFright.UI.Pages.Dashboard.Dashboard>();

        var extensionsSection = cut.Find("[aria-label='Extensions']");
        Assert.NotNull(extensionsSection.QuerySelector(".card.tile-test-tile.tile-size-1x2"));
    }

    private static IDashboardTileProvider MakeLinkedProvider(
        string id, int displayOrder, string route, string actionText)
    {
        var p = MakeCoreProvider(id, displayOrder);
        p.NavigateRoute.Returns(route);
        p.ActionText.Returns(actionText);
        return p;
    }

    private void SetupProviders(params IDashboardTileProvider[] providers)
    {
        var list = (IReadOnlyList<IDashboardTileProvider>)providers.ToList();
        _dashboardService.GetTilesAsync(Arg.Any<CancellationToken>())
            .Returns(list);

        foreach (var p in providers)
        {
            var result = new TileLoadResult(p, new TileData(), null);
            _dashboardService.LoadTileAsync(p, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(result));
        }
    }

    private static IDashboardTileProvider MakeCoreProvider(string id, int displayOrder)
    {
        var p = Substitute.For<IDashboardTileProvider>();
        p.TileId.Returns(id);
        p.Title.Returns(id);
        p.ModuleName.Returns(id);
        p.DisplayOrder.Returns(displayOrder);
        p.TileComponentType.Returns(typeof(object));
        return p;
    }

    private static IDashboardTileProvider MakeSizedProvider(string id, int displayOrder, DashboardTileSize size)
    {
        var p = MakeCoreProvider(id, displayOrder);
        p.TileSize.Returns(size);
        return p;
    }

    private static IDashboardTileProvider MakePluginProvider(string id, int displayOrder)
    {
        var p = Substitute.For<IDashboardTileProvider>();
        p.TileId.Returns(id);
        p.Title.Returns(id);
        p.ModuleName.Returns("TestPlugin");
        p.DisplayOrder.Returns(displayOrder);
        p.TileComponentType.Returns(typeof(object));
        return p;
    }
}
