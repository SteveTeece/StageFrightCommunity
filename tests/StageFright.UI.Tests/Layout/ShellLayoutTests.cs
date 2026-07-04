using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Plugins.Contracts;
using StageFright.UI.Layout;

namespace StageFright.UI.Tests.Layout;

/// <summary>
/// bUnit tests for ShellLayout — verifies the floating navigation dock renders one
/// item per provider menu item with the correct glyph and short label, flyout
/// submenus, badges, active-route highlighting, navigation, and the theme toggle.
/// </summary>
public class ShellLayoutTests : BunitContext
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    public ShellLayoutTests()
    {
        Services.AddSingleton(_settingsService);
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns((Settings?)null);
    }

    // --- Dock items ---

    [Fact]
    public void Should_RenderDockItemPerMenuItem_When_ProvidersRegistered()
    {
        AddProvider(0,
            new MenuItem { Title = "Dashboard", Route = "/dashboard", ShortLabel = "HOME" },
            new MenuItem { Title = "Members", Route = "/members", ShortLabel = "MEMB" });

        var cut = Render<ShellLayout>();

        Assert.Equal(2, cut.FindAll(".dock-link").Count);
    }

    [Fact]
    public void Should_UseProviderShortLabel_When_ShortLabelIsSet()
    {
        AddProvider(0, new MenuItem { Title = "Dashboard", Route = "/dashboard", ShortLabel = "HOME" });

        var cut = Render<ShellLayout>();

        Assert.Equal("HOME", cut.Find(".dock-label").TextContent);
    }

    [Fact]
    public void Should_DeriveShortLabelFromTitle_When_ShortLabelIsMissing()
    {
        AddProvider(0, new MenuItem { Title = "Costumes", Route = "/costumes" });

        var cut = Render<ShellLayout>();

        Assert.Equal("COST", cut.Find(".dock-label").TextContent);
    }

    [Fact]
    public void Should_UseWholeTitleUppercased_When_TitleIsFourCharsOrFewer()
    {
        AddProvider(0, new MenuItem { Title = "Hub", Route = "/hub" });

        var cut = Render<ShellLayout>();

        Assert.Equal("HUB", cut.Find(".dock-label").TextContent);
    }

    // --- Glyph mapping ---

    [Theory]
    [InlineData("/dashboard", "glyph-home")]
    [InlineData("/members", "glyph-members")]
    [InlineData("/finance", "glyph-finance")]
    [InlineData("/rehearsals", "glyph-rehearsals")]
    [InlineData("/events", "glyph-events")]
    [InlineData("/reports", "glyph-reports")]
    [InlineData("/settings", "glyph-settings")]
    public void Should_MapRouteToModuleGlyph_When_RouteIsKnown(string route, string expectedGlyph)
    {
        AddProvider(0, new MenuItem { Title = "Item", Route = route });

        var cut = Render<ShellLayout>();

        Assert.Contains(expectedGlyph, cut.Find(".dock-glyph").ClassList);
    }

    [Fact]
    public void Should_UsePluginGlyph_When_RouteIsUnknown()
    {
        AddProvider(0, new MenuItem { Title = "Costumes", Route = "/costumes" });

        var cut = Render<ShellLayout>();

        Assert.Contains("glyph-plugin", cut.Find(".dock-glyph").ClassList);
    }

    // --- Flyout submenus ---

    [Fact]
    public void Should_RenderFlyoutWithSubItems_When_ItemHasSubItems()
    {
        AddProvider(0, new MenuItem
        {
            Title = "Members",
            Route = "/members",
            SubItems =
            [
                new MenuItem { Title = "Active Members", Route = "/members" },
                new MenuItem { Title = "Add Member", Route = "/members/new" }
            ]
        });

        var cut = Render<ShellLayout>();

        var flyoutLinks = cut.FindAll(".dock-flyout .dock-flyout-link");
        Assert.Equal(2, flyoutLinks.Count);
        Assert.Contains(flyoutLinks, l => l.TextContent.Contains("Add Member"));
    }

    [Fact]
    public void Should_NotRenderFlyout_When_ItemHasNoSubItems()
    {
        AddProvider(0, new MenuItem { Title = "Settings", Route = "/settings" });

        var cut = Render<ShellLayout>();

        Assert.Empty(cut.FindAll(".dock-flyout"));
    }

    [Fact]
    public void Should_RenderNestedFlyout_When_SubItemHasSubItems()
    {
        AddProvider(0, new MenuItem
        {
            Title = "Reports",
            Route = "/reports",
            SubItems =
            [
                new MenuItem
                {
                    Title = "Finance",
                    Route = "/reports/finance",
                    SubItems = [new MenuItem { Title = "Trial Balance", Route = "/reports/trial-balance" }]
                }
            ]
        });

        var cut = Render<ShellLayout>();

        Assert.Contains(cut.FindAll(".dock-flyout-link"), l => l.TextContent.Contains("Trial Balance"));
    }

    // --- Badges ---

    [Fact]
    public void Should_RenderBadge_When_BadgeTextIsSet()
    {
        AddProvider(0, new MenuItem { Title = "Members", Route = "/members", BadgeText = "3" });

        var cut = Render<ShellLayout>();

        Assert.Equal("3", cut.Find(".dock-badge").TextContent);
    }

    [Fact]
    public void Should_NotRenderBadge_When_BadgeTextIsNull()
    {
        AddProvider(0, new MenuItem { Title = "Members", Route = "/members" });

        var cut = Render<ShellLayout>();

        Assert.Empty(cut.FindAll(".dock-badge"));
    }

    // --- Active route ---

    [Fact]
    public void Should_MarkDockLinkActive_When_CurrentUriMatchesRoute()
    {
        AddProvider(0,
            new MenuItem { Title = "Dashboard", Route = "/dashboard" },
            new MenuItem { Title = "Members", Route = "/members" });
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("/members");

        var cut = Render<ShellLayout>();

        var links = cut.FindAll(".dock-link");
        Assert.DoesNotContain("active", links[0].ClassList);
        Assert.Contains("active", links[1].ClassList);
    }

    // --- Navigation ---

    [Fact]
    public void Should_NavigateToRoute_When_DockLinkClicked()
    {
        AddProvider(0, new MenuItem { Title = "Members", Route = "/members" });
        var nav = Services.GetRequiredService<BunitNavigationManager>();

        var cut = Render<ShellLayout>();
        cut.Find(".dock-link").Click();

        Assert.EndsWith("/members", nav.Uri);
    }

    [Fact]
    public void Should_NavigateToSubItemRoute_When_FlyoutLinkClicked()
    {
        AddProvider(0, new MenuItem
        {
            Title = "Members",
            Route = "/members",
            SubItems = [new MenuItem { Title = "Add Member", Route = "/members/new" }]
        });
        var nav = Services.GetRequiredService<BunitNavigationManager>();

        var cut = Render<ShellLayout>();
        cut.Find(".dock-flyout-link").Click();

        Assert.EndsWith("/members/new", nav.Uri);
    }

    // --- Provider ordering ---

    [Fact]
    public void Should_OrderDockItemsByProviderDisplayOrder_When_MultipleProvidersRegistered()
    {
        AddProvider(5, new MenuItem { Title = "Reports", Route = "/reports", ShortLabel = "RPT" });
        AddProvider(0, new MenuItem { Title = "Dashboard", Route = "/dashboard", ShortLabel = "HOME" });

        var cut = Render<ShellLayout>();

        var labels = cut.FindAll(".dock-label");
        Assert.Equal("HOME", labels[0].TextContent);
        Assert.Equal("RPT", labels[1].TextContent);
    }

    // --- Theme toggle ---

    [Fact]
    public void Should_RenderThemeToggleShowingLight_When_ThemeIsLight()
    {
        AddProvider(0, new MenuItem { Title = "Dashboard", Route = "/dashboard" });

        var cut = Render<ShellLayout>();

        Assert.Contains("Light", cut.Find(".btn-theme-toggle").TextContent);
    }

    [Fact]
    public void Should_SwitchToDarkTheme_When_ToggleClicked()
    {
        AddProvider(0, new MenuItem { Title = "Dashboard", Route = "/dashboard" });

        var cut = Render<ShellLayout>();
        cut.Find(".btn-theme-toggle").Click();

        Assert.Equal("dark", cut.Find("[data-bs-theme]").GetAttribute("data-bs-theme"));
        Assert.Contains("Dark", cut.Find(".btn-theme-toggle").TextContent);
    }

    [Fact]
    public void Should_RenderDockLogoAndAvatar_When_LayoutRenders()
    {
        AddProvider(0, new MenuItem { Title = "Dashboard", Route = "/dashboard" });

        var cut = Render<ShellLayout>();

        Assert.NotNull(cut.Find(".dock-logo"));
        Assert.Equal("SF", cut.Find(".dock-avatar").TextContent);
    }

    // --- Helpers ---

    private void AddProvider(int displayOrder, params MenuItem[] items)
    {
        var provider = Substitute.For<IMenuItemProvider>();
        provider.DisplayOrder.Returns(displayOrder);
        provider.GetMenuItems().Returns(items);
        Services.AddSingleton(provider);
    }
}
