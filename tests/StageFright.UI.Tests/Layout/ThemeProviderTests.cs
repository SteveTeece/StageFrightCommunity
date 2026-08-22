using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.UI.Layout;

namespace StageFright.UI.Tests.Layout;

/// <summary>
/// bUnit tests for ThemeProvider — verifies data-bs-theme attribute changes on toggle,
/// the OS-preference-driven fallback (Dark when unspecified), and preference persistence
/// via SettingsService.
/// </summary>
public class ThemeProviderTests : BunitContext
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IDeviceThemePreferenceProvider _deviceThemeProvider = Substitute.For<IDeviceThemePreferenceProvider>();

    public ThemeProviderTests()
    {
        Services.AddSingleton(_settingsService);
        Services.AddSingleton(_deviceThemeProvider);
        _deviceThemeProvider.GetPreference().Returns(PlatformThemePreference.Unspecified);
    }

    // --- Default theme (no Settings row yet — pre-setup fallback) ---

    [Theory]
    [InlineData(PlatformThemePreference.Light, "light")]
    [InlineData(PlatformThemePreference.Dark, "dark")]
    [InlineData(PlatformThemePreference.Unspecified, "dark")]
    public void Renders_DataBsTheme_FromDevicePreference_WhenSettingsNull(PlatformThemePreference preference, string expectedAttr)
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns((Settings?)null);
        _deviceThemeProvider.GetPreference().Returns(preference);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span class='child'>Content</span>"));

        var attr = cut.Find("[data-bs-theme]").GetAttribute("data-bs-theme");
        Assert.Equal(expectedAttr, attr);
    }

    [Fact]
    public async Task Renders_DataBsTheme_Light_WhenSettingsThemeIsLight()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(Theme.Light));
        _settingsService.SaveAsync(Arg.Any<StageFright.Core.Entities.Settings>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span class='child'>Content</span>"));

        await cut.InvokeAsync(() => cut.Instance.ToggleAsync()); // toggle to dark
        await cut.InvokeAsync(() => cut.Instance.ToggleAsync()); // toggle back to light

        var attr = cut.Find("[data-bs-theme]").GetAttribute("data-bs-theme");
        Assert.Equal("light", attr);
    }

    [Fact]
    public void Renders_DataBsTheme_Dark_WhenSettingsThemeIsDark()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(Theme.Dark));

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span class='child'>Content</span>"));

        var attr = cut.Find("[data-bs-theme]").GetAttribute("data-bs-theme");
        Assert.Equal("dark", attr);
    }

    [Theory]
    [InlineData(PlatformThemePreference.Light, "light")]
    [InlineData(PlatformThemePreference.Dark, "dark")]
    [InlineData(PlatformThemePreference.Unspecified, "dark")]
    public void Renders_DataBsTheme_FromDevicePreference_WhenSettingsThrows(PlatformThemePreference preference, string expectedAttr)
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns<Settings?>(_ => throw new InvalidOperationException("DB unavailable"));
        _deviceThemeProvider.GetPreference().Returns(preference);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span class='child'>Content</span>"));

        var attr = cut.Find("[data-bs-theme]").GetAttribute("data-bs-theme");
        Assert.Equal(expectedAttr, attr);
    }

    // --- Toggle ---

    [Fact]
    public async Task Toggle_ChangesTheme_FromLight_ToDark()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(Theme.Light));
        _settingsService.SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span>Content</span>"));

        Assert.Equal("light", cut.Find("[data-bs-theme]").GetAttribute("data-bs-theme"));

        await cut.InvokeAsync(() => cut.Instance.ToggleAsync());

        Assert.Equal("dark", cut.Find("[data-bs-theme]").GetAttribute("data-bs-theme"));
    }

    [Fact]
    public async Task Toggle_ChangesTheme_FromDark_ToLight()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(Theme.Dark));
        _settingsService.SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span>Content</span>"));

        Assert.Equal("dark", cut.Find("[data-bs-theme]").GetAttribute("data-bs-theme"));

        await cut.InvokeAsync(() => cut.Instance.ToggleAsync());

        Assert.Equal("light", cut.Find("[data-bs-theme]").GetAttribute("data-bs-theme"));
    }

    // --- Persistence ---

    [Fact]
    public async Task Toggle_PersistsTheme_ViaSaveAsync()
    {
        var settings = MakeSettings(Theme.Light);
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(settings);
        _settingsService.SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span>Content</span>"));

        await cut.InvokeAsync(() => cut.Instance.ToggleAsync());

        await _settingsService.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s!.Theme == Theme.Dark),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Toggle_WhenSettingsNull_DoesNotCallSaveAsync()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns((Settings?)null);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span>Content</span>"));

        await cut.InvokeAsync(() => cut.Instance.ToggleAsync());

        await _settingsService.DidNotReceive().SaveAsync(
            Arg.Any<Settings>(), Arg.Any<CancellationToken>());
    }

    // --- Cascading value ---

    [Fact]
    public void ExposesItself_AsCascadingValue_SoChildrenCanAccess()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns((Settings?)null);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span>Content</span>"));

        // The ThemeProvider instance is accessible as the component instance
        Assert.NotNull(cut.Instance);
        Assert.IsType<ThemeProvider>(cut.Instance);
    }

    // --- CurrentTheme ---

    [Theory]
    [InlineData(PlatformThemePreference.Light, Theme.Light)]
    [InlineData(PlatformThemePreference.Dark, Theme.Dark)]
    [InlineData(PlatformThemePreference.Unspecified, Theme.Dark)]
    public void CurrentTheme_FollowsDevicePreference_WhenSettingsNull(PlatformThemePreference preference, Theme expectedTheme)
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns((Settings?)null);
        _deviceThemeProvider.GetPreference().Returns(preference);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span>Content</span>"));

        Assert.Equal(expectedTheme, cut.Instance.CurrentTheme);
    }

    [Fact]
    public async Task CurrentTheme_UpdatesAfterToggle()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(Theme.Light));
        _settingsService.SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var cut = Render<ThemeProvider>(p =>
            p.AddChildContent("<span>Content</span>"));

        Assert.Equal(Theme.Light, cut.Instance.CurrentTheme);

        await cut.InvokeAsync(() => cut.Instance.ToggleAsync());

        Assert.Equal(Theme.Dark, cut.Instance.CurrentTheme);
    }

    // --- Helpers ---

    private static Settings MakeSettings(Theme theme) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationName = "Test Org",
        AnnualFee = 50m,
        AttendanceFee = 5m,
        MembershipRenewalMonth = 1,
        Theme = theme,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
