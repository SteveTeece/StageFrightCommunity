using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.UI.Tests;

/// <summary>
/// bUnit tests for <see cref="App"/>'s startup routing decision (spec 029, US1; spec 030, US1): a
/// startup error takes priority over everything; while setup is incomplete, a missing recorded
/// language preference routes to <c>/language-select</c> and a recorded one routes to
/// <c>/first-run-restore</c> — the pre-wizard restore choice — never to <c>/setup</c> directly;
/// once setup is complete there is no redirect, regardless of what the preference store holds.
/// </summary>
public class AppRoutingTests : LocalizedTestContext
{
    private readonly IStartupDiagnosticService _diagnostics = Substitute.For<IStartupDiagnosticService>();
    private readonly ISetupService _setupService = Substitute.For<ISetupService>();
    private readonly ILanguagePreferenceStore _preferenceStore = Substitute.For<ILanguagePreferenceStore>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IDeviceThemePreferenceProvider _deviceThemeProvider = Substitute.For<IDeviceThemePreferenceProvider>();
    private readonly IBackupService _backupService = Substitute.For<IBackupService>();

    public AppRoutingTests()
    {
        Services.AddSingleton(_diagnostics);
        Services.AddSingleton(_setupService);
        Services.AddSingleton(_preferenceStore);
        Services.AddSingleton(_settingsService);
        Services.AddSingleton(_deviceThemeProvider);
        Services.AddSingleton(_backupService); // FirstRunRestoreScreen (the /first-run-restore redirect target) injects it
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _deviceThemeProvider.GetPreference().Returns(PlatformThemePreference.Dark);
        _diagnostics.HasStartupError.Returns(false);

        // Kept defensively: harmless if a redirect target ever mounts a BlazorBootstrap <Tabs>
        // (the current first-run targets — /language-select, /first-run-restore — do not).
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
    public void NavigatesToFirstRunRestore_When_SetupIncomplete_AndPreferenceRecorded()
    {
        _setupService.IsSetupCompleteAsync(Arg.Any<CancellationToken>()).Returns(false);
        _preferenceStore.Get().Returns("fr-FR");

        Render<App>();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/first-run-restore", nav.Uri); // spec 030 FR-001 — the pre-wizard restore choice
        Assert.DoesNotContain("/setup", nav.Uri);       // /setup is never the direct first-run target
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
