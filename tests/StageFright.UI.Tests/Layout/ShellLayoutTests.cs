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
/// bUnit tests for ShellLayout — verifies the fixed vertical sidebar renders one
/// link per top-level provider menu item with the correct icon and full title,
/// renders sub-items as expandable groups (auto-expanding on active child routes),
/// and handles badges, active-route highlighting, navigation, and the theme toggle.
/// </summary>
public class ShellLayoutTests : BunitContext
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IDeviceThemePreferenceProvider _deviceThemeProvider = Substitute.For<IDeviceThemePreferenceProvider>();

    public ShellLayoutTests()
    {
        Services.AddSingleton(_settingsService);
        Services.AddSingleton(_deviceThemeProvider);
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns((Settings?)null);
        _deviceThemeProvider.GetPreference().Returns(PlatformThemePreference.Light);
    }

    // --- Sidebar items ---

    [Fact]
    public void Should_RenderSidebarLinkPerMenuItem_When_ProvidersRegistered()
    {
        AddProvider(0,
            new MenuItem { Title = "Dashboard", Route = "/dashboard" },
            new MenuItem { Title = "Members", Route = "/members" });

        var cut = Render<ShellLayout>();

        Assert.Equal(2, cut.FindAll(".sidebar-link").Count);
    }

    [Fact]
    public void Should_RenderFullTitleAsLabel_When_ItemRenders()
    {
        AddProvider(0, new MenuItem { Title = "Rehearsals", Route = "/rehearsals", ShortLabel = "REH" });

        var cut = Render<ShellLayout>();

        Assert.Equal("Rehearsals", cut.Find(".sidebar-label").TextContent);
    }

    // --- Icon mapping ---

    [Theory]
    [InlineData("/dashboard", "icon-dashboard")]
    [InlineData("/members", "icon-members")]
    [InlineData("/finance", "icon-finance")]
    [InlineData("/rehearsals", "icon-rehearsals")]
    [InlineData("/events", "icon-events")]
    [InlineData("/reports", "icon-reports")]
    [InlineData("/settings", "icon-settings")]
    public void Should_MapRouteToModuleIcon_When_RouteIsKnown(string route, string expectedIcon)
    {
        AddProvider(0, new MenuItem { Title = "Item", Route = route });

        var cut = Render<ShellLayout>();

        Assert.Contains(expectedIcon, cut.Find(".nav-icon").ClassList);
    }

    [Fact]
    public void Should_UsePluginIcon_When_RouteIsUnknown()
    {
        AddProvider(0, new MenuItem { Title = "Costumes", Route = "/costumes" });

        var cut = Render<ShellLayout>();

        Assert.Contains("icon-plugin", cut.Find(".nav-icon").ClassList);
    }

    // --- Sub-item groups ---

    private static MenuItem FinanceGroup() => new()
    {
        Title = "Finance",
        Route = "/finance",
        SubItems =
        [
            new MenuItem { Title = "Overview", Route = "/finance", DisplayOrder = 0 },
            new MenuItem { Title = "Chart of Accounts", Route = "/finance/accounts", DisplayOrder = 1 }
        ]
    };

    [Fact]
    public void Should_RenderCollapsedGroup_When_ItemHasSubItemsAndNoChildActive()
    {
        AddProvider(0, FinanceGroup());
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("/dashboard");

        var cut = Render<ShellLayout>();

        Assert.NotNull(cut.Find(".sidebar-group-header"));
        Assert.Empty(cut.FindAll(".sidebar-sublist"));
        Assert.DoesNotContain("Chart of Accounts", cut.Markup);
    }

    [Fact]
    public void Should_ExpandGroup_When_ChevronClicked()
    {
        AddProvider(0, FinanceGroup());
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("/dashboard");

        var cut = Render<ShellLayout>();
        cut.Find(".sidebar-chevron").Click();

        Assert.NotNull(cut.Find(".sidebar-sublist"));
        Assert.Contains("Chart of Accounts", cut.Markup);
    }

    [Fact]
    public void Should_CollapseGroup_When_ChevronClickedTwice()
    {
        AddProvider(0, FinanceGroup());
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("/dashboard");

        var cut = Render<ShellLayout>();
        cut.Find(".sidebar-chevron").Click();
        cut.Find(".sidebar-chevron").Click();

        Assert.Empty(cut.FindAll(".sidebar-sublist"));
    }

    [Fact]
    public void Should_AutoExpandGroup_When_ChildRouteIsActive()
    {
        AddProvider(0, FinanceGroup());
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("/finance/accounts");

        var cut = Render<ShellLayout>();

        Assert.NotNull(cut.Find(".sidebar-sublist"));
        var subLinks = cut.FindAll(".sidebar-sublink");
        Assert.Contains(subLinks, l => l.TextContent.Contains("Chart of Accounts") && l.ClassList.Contains("active"));
    }

    [Fact]
    public void Should_MarkGroupHeaderActive_When_ChildRouteIsActive()
    {
        AddProvider(0, FinanceGroup());
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("/finance/accounts");

        var cut = Render<ShellLayout>();

        Assert.Contains("active", cut.Find(".sidebar-group-header").ClassList);
    }

    [Fact]
    public void Should_NavigateToSubRoute_When_SubLinkClicked()
    {
        AddProvider(0, FinanceGroup());
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/finance");

        var cut = Render<ShellLayout>();
        var subLink = cut.FindAll(".sidebar-sublink").First(l => l.TextContent.Contains("Chart of Accounts"));
        subLink.Click();

        Assert.EndsWith("/finance/accounts", nav.Uri);
    }

    [Fact]
    public void Should_NavigateToParentRoute_When_GroupHeaderLinkClicked()
    {
        AddProvider(0, FinanceGroup());
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/dashboard");

        var cut = Render<ShellLayout>();
        cut.Find(".sidebar-group-link").Click();

        Assert.EndsWith("/finance", nav.Uri);
    }

    // --- Badges ---

    [Fact]
    public void Should_RenderBadge_When_BadgeTextIsSet()
    {
        AddProvider(0, new MenuItem { Title = "Members", Route = "/members", BadgeText = "3" });

        var cut = Render<ShellLayout>();

        Assert.Equal("3", cut.Find(".sidebar-badge").TextContent);
    }

    [Fact]
    public void Should_NotRenderBadge_When_BadgeTextIsNull()
    {
        AddProvider(0, new MenuItem { Title = "Members", Route = "/members" });

        var cut = Render<ShellLayout>();

        Assert.Empty(cut.FindAll(".sidebar-badge"));
    }

    // --- Active route ---

    [Fact]
    public void Should_MarkSidebarLinkActive_When_CurrentUriMatchesRoute()
    {
        AddProvider(0,
            new MenuItem { Title = "Dashboard", Route = "/dashboard" },
            new MenuItem { Title = "Members", Route = "/members" });
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("/members");

        var cut = Render<ShellLayout>();

        var links = cut.FindAll(".sidebar-link");
        Assert.DoesNotContain("active", links[0].ClassList);
        Assert.Contains("active", links[1].ClassList);
    }

    // --- Navigation ---

    [Fact]
    public void Should_NavigateToRoute_When_SidebarLinkClicked()
    {
        AddProvider(0, new MenuItem { Title = "Members", Route = "/members" });
        var nav = Services.GetRequiredService<BunitNavigationManager>();

        var cut = Render<ShellLayout>();
        cut.Find(".sidebar-link").Click();

        Assert.EndsWith("/members", nav.Uri);
    }

    // --- Provider ordering ---

    [Fact]
    public void Should_OrderSidebarItemsByProviderDisplayOrder_When_MultipleProvidersRegistered()
    {
        AddProvider(5, new MenuItem { Title = "Reports", Route = "/reports" });
        AddProvider(0, new MenuItem { Title = "Dashboard", Route = "/dashboard" });

        var cut = Render<ShellLayout>();

        var labels = cut.FindAll(".sidebar-label");
        Assert.Equal("Dashboard", labels[0].TextContent);
        Assert.Equal("Reports", labels[1].TextContent);
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
        cut.Find(".btn-theme-toggle [role=switch]").Click();

        Assert.Equal("dark", cut.Find("[data-bs-theme]").GetAttribute("data-bs-theme"));
        Assert.Contains("Dark", cut.Find(".btn-theme-toggle").TextContent);
    }

    [Fact]
    public void Should_NotRenderThemeToggle_When_OnSetupRoute()
    {
        AddProvider(0, new MenuItem { Title = "Dashboard", Route = "/dashboard" });
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("/setup");

        var cut = Render<ShellLayout>();

        Assert.Empty(cut.FindAll(".btn-theme-toggle"));
    }

    // --- Brand ---

    [Fact]
    public void Should_RenderBrand_When_LayoutRenders()
    {
        AddProvider(0, new MenuItem { Title = "Dashboard", Route = "/dashboard" });

        var cut = Render<ShellLayout>();

        Assert.NotNull(cut.Find(".sidebar-logo"));
        Assert.Equal("StageFright", cut.Find(".sidebar-brand-text").TextContent);
        Assert.Empty(cut.FindAll(".sidebar-avatar"));
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
