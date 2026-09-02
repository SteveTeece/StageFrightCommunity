using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.UI.Tests;

/// <summary>
/// bUnit tests for <see cref="App"/>'s startup routing decision (spec 029, US1): a startup error
/// takes priority over everything; while setup is incomplete, a missing recorded language
/// preference routes to <c>/language-select</c> and a recorded one routes straight to
/// <c>/setup</c> (unchanged target, new guard); once setup is complete there is no redirect to
/// either, regardless of what the preference store holds.
/// </summary>
public class AppRoutingTests : LocalizedTestContext
{
    private readonly IStartupDiagnosticService _diagnostics = Substitute.For<IStartupDiagnosticService>();
    private readonly ISetupService _setupService = Substitute.For<ISetupService>();
    private readonly ILanguagePreferenceStore _preferenceStore = Substitute.For<ILanguagePreferenceStore>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IDeviceThemePreferenceProvider _deviceThemeProvider = Substitute.For<IDeviceThemePreferenceProvider>();

    public AppRoutingTests()
    {
        Services.AddSingleton(_diagnostics);
        Services.AddSingleton(_setupService);
        Services.AddSingleton(_preferenceStore);
        Services.AddSingleton(_settingsService);
        Services.AddSingleton(_deviceThemeProvider);
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _deviceThemeProvider.GetPreference().Returns(PlatformThemePreference.Dark);
        _diagnostics.HasStartupError.Returns(false);

        // The /setup redirect target mounts SetupWizard, which hosts a BlazorBootstrap <Tabs> —
        // same JS-interop stubbing SetupWizardTests/SetupWizardThemeTests already need.
        JSInterop.SetupVoid("window.blazorBootstrap.tabs.initialize", _ => true);
        JSInterop.SetupVoid("window.blazorBootstrap.tabs.show", _ => true);
        JSInterop.SetupVoid("window.blazorBootstrap.tabs.dispose", _ => true);
    }

    [Fact]
    public void NavigatesToStartupError_When_HasStartupError_RegardlessOfSetupOrPreference()
    {
        _diagnostics.HasStartupError.Returns(true);
        _setupService.IsSetupCompleteAsync(Arg.Any<CancellationToken>()).Returns(false);
        _preferenceStore.Get().Returns((string?)null);

        Render<App>();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/startup-error", nav.Uri);
    }

    [Fact]
    public void NavigatesToLanguageSelect_When_SetupIncomplete_AndNoRecordedPreference()
    {
        _setupService.IsSetupCompleteAsync(Arg.Any<CancellationToken>()).Returns(false);
        _preferenceStore.Get().Returns((string?)null);

        Render<App>();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/language-select", nav.Uri);
    }

    [Fact]
    public void NavigatesStraightToSetup_When_SetupIncomplete_AndPreferenceRecorded()
    {
        _setupService.IsSetupCompleteAsync(Arg.Any<CancellationToken>()).Returns(false);
        _preferenceStore.Get().Returns("fr-FR");

        Render<App>();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/setup", nav.Uri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("fr-FR")]
    public void DoesNotRedirect_When_SetupAlreadyComplete(string? recordedPreference)
    {
        _setupService.IsSetupCompleteAsync(Arg.Any<CancellationToken>()).Returns(true);
        _preferenceStore.Get().Returns(recordedPreference);
        var initialUri = Services.GetRequiredService<NavigationManager>().Uri;

        Render<App>();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal(initialUri, nav.Uri);
    }
}
