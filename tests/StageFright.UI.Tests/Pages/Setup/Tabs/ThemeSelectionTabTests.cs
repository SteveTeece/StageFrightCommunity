using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.UI.Layout;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup.Tabs;

/// <summary>bUnit tests for ThemeSelectionTab (US6) — the Light/Dark theme dropdown,
/// split out of GeneralAppearanceTab so it can be rendered lower in the Organisation
/// Settings tab, directly above the Sales Tax section.</summary>
public class ThemeSelectionTabTests : BunitContext
{
    public ThemeSelectionTabTests()
    {
        Services.AddSingleton(Substitute.For<ISettingsService>());
        Services.AddSingleton(Substitute.For<IDeviceThemePreferenceProvider>());
    }

    [Fact]
    public void RendersThemeDropdown()
    {
        var cut = Render<ThemeProvider>(p => p.AddChildContent<ThemeSelectionTab>());

        cut.Find("#themeSelect");
    }
}
