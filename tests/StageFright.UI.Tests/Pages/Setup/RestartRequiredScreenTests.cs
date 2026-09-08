using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.UI.Layout;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Tests.Pages.Setup;

/// <summary>
/// bUnit tests for <see cref="RestartRequiredScreen"/> and its chrome-free <see cref="BlankLayout"/>
/// (spec 030, US1 / FR-007): the screen shows a single close-and-reopen instruction and renders no
/// control that routes anywhere, and <see cref="BlankLayout"/> emits no sidebar / nav — so a
/// post-restore user has no navigation surface into pre-restore in-memory state.
/// </summary>
public class RestartRequiredScreenTests : LocalizedTestContext
{
    public RestartRequiredScreenTests()
    {
        // BlankLayout renders CultureProvider/ThemeProvider; ThemeProvider needs the device theme
        // seam (LocalizedTestContext already supplies a null-returning ISettingsService).
        Services.AddSingleton(Substitute.For<IDeviceThemePreferenceProvider>());
    }

    [Fact]
    public void RendersTheCloseAndReopenInstruction()
    {
        var cut = Render<RestartRequiredScreen>();

        var expected = Services.GetRequiredService<IStringLocalizer<SetupResource>>()["Setup_RestartRequired_Instruction"];
        Assert.Contains(expected, cut.Markup);
    }

    [Fact]
    public void RendersNoControlThatRoutesAwayFromTheScreen()
    {
        var cut = Render<RestartRequiredScreen>();

        Assert.Empty(cut.FindAll("a"));
        Assert.Empty(cut.FindAll("button"));
        foreach (var route in new[] { "/dashboard", "/setup", "/first-run-restore" })
            Assert.DoesNotContain(route, cut.Markup);
    }

    [Fact]
    public void BlankLayout_EmitsNoSidebarOrNav()
    {
        var cut = Render<BlankLayout>(parameters => parameters.Add(
            p => p.Body,
            (RenderFragment)(builder => builder.AddMarkupContent(0, "<p data-testid=\"stub-body\">stub</p>"))));

        Assert.Empty(cut.FindAll(".shell-sidebar"));
        Assert.Empty(cut.FindAll("nav"));
        Assert.Empty(cut.FindAll("a"));
        Assert.Contains("stub-body", cut.Markup);
    }
}
