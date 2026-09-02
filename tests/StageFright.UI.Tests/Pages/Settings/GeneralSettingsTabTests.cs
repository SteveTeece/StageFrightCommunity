using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.UI.Layout;
using StageFright.UI.Pages.Settings;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Settings;

/// <summary>
/// bUnit tests for GeneralSettingsTab after the ABN/GST removal (spec 016): the ABN field
/// and GST controls are both gone, and HandleSaveAsync merges tax-owned fields from a
/// fresh fetch before saving (FR-008/cross-tab save safety). Spec 029 (US2) adds: a changed
/// language selection applies to the running session immediately on save (no restart notice
/// ever appears), via the same ILanguagePreferenceStore.Set + CultureProvider.Switch sequence
/// the first-run screen uses.
/// </summary>
public class GeneralSettingsTabTests : LocalizedTestContext
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly ILanguagePreferenceStore _languagePreferenceStore = Substitute.For<ILanguagePreferenceStore>();

    public GeneralSettingsTabTests()
    {
        Services.AddSingleton(_settingsService);
        Services.AddSingleton(_languagePreferenceStore);
    }

    private IRenderedComponent<CultureProvider> RenderTabUnderCulture() =>
        Render<CultureProvider>(p => p.AddChildContent<GeneralSettingsTab>());

    private static AppSettings MakeSettings() => new()
    {
        Id = Guid.NewGuid(),
        OrganizationName = "Test Org",
        AnnualFee = 75m,
        AttendanceFee = 5m,
        MembershipRenewalMonth = 1,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public void ThemeSwitch_IsUnchecked_WhenThemeIsLight()
    {
        var settings = MakeSettings();
        settings.Theme = Theme.Light;
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var cut = Render<GeneralSettingsTab>();

        var themeSwitch = cut.Find("#theme-toggle-switch");
        Assert.Equal("false", themeSwitch.GetAttribute("aria-checked"));
    }

    [Fact]
    public void ThemeSwitch_IsChecked_WhenThemeIsDark()
    {
        var settings = MakeSettings();
        settings.Theme = Theme.Dark;
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var cut = Render<GeneralSettingsTab>();

        var themeSwitch = cut.Find("#theme-toggle-switch");
        Assert.Equal("true", themeSwitch.GetAttribute("aria-checked"));
    }

    [Fact]
    public void TaxControls_AreAbsent()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<GeneralSettingsTab>();

        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#taxApplicable"));
        Assert.DoesNotContain("GST / BAS", cut.Markup);
        Assert.DoesNotContain("Sales Tax", cut.Markup);
    }

    [Fact]
    public void AbnField_IsAbsent()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<GeneralSettingsTab>();

        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#abn"));
        Assert.DoesNotContain("ABN", cut.Markup);
    }

    [Fact]
    public async Task Save_Succeeds()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());
        _settingsService.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var cut = Render<GeneralSettingsTab>();
        await cut.Find("form").SubmitAsync();

        await _settingsService.Received(1).SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
        Assert.Contains("Settings saved successfully", cut.Markup);
    }

    [Fact]
    public async Task HandleSaveAsync_MergesTaxFields_FromFreshFetch()
    {
        var loaded = MakeSettings();
        var freshFromDb = MakeSettings();
        freshFromDb.IsTaxApplicable = true;
        freshFromDb.TaxRate = 10m;
        freshFromDb.AnnualFeeTaxCode = TaxCode.Taxable;
        freshFromDb.AttendanceFeeTaxCode = TaxCode.TaxExempt;

        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(loaded, freshFromDb);
        _settingsService.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var cut = Render<GeneralSettingsTab>();
        await cut.Find("form").SubmitAsync();

        await _settingsService.Received(1).SaveAsync(
            Arg.Is<AppSettings>(s =>
                s!.IsTaxApplicable &&
                s.AnnualFeeTaxCode == TaxCode.Taxable &&
                s.AttendanceFeeTaxCode == TaxCode.TaxExempt),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AuditRetentionDropdown_RendersSevenOptions()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<GeneralSettingsTab>();

        var select = cut.Find("#auditRetentionYears");
        Assert.Equal(7, select.Children.Length);
    }

    [Fact]
    public async Task Save_PersistsSelectedAuditRetentionYears()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());
        _settingsService.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var cut = Render<GeneralSettingsTab>();
        cut.Find("#auditRetentionYears").Change("7");
        await cut.Find("form").SubmitAsync();

        await _settingsService.Received(1).SaveAsync(
            Arg.Is<AppSettings>(s => s!.AuditRetentionYears == 7),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_ShowsError_WhenAuditRetentionValidationFails()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());
        _settingsService.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new StageFright.Core.Exceptions.ValidationException(
                "Audit retention period must be between 1 and 7 years.", "Settings", "SaveAsync"));

        var cut = Render<GeneralSettingsTab>();
        await cut.Find("form").SubmitAsync();

        Assert.Contains("Audit retention period must be between 1 and 7 years.", cut.Markup);
    }

    // --- spec 029 US2: language save applies immediately, no restart notice ---

    [Fact]
    public async Task Save_WithChangedLanguage_RecordsThePreference_AndSwitchesTheRunningSessionCulture()
    {
        using var _ = new CultureRestorer();
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());
        _settingsService.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var cut = RenderTabUnderCulture();
        var tab = cut.FindComponent<GeneralSettingsTab>();
        tab.Find("#languageCode").Change("fr-FR");

        await tab.Find("form").SubmitAsync();

        _languagePreferenceStore.Received(1).Set("fr-FR");
        Assert.Equal("fr-FR", cut.Instance.CurrentCulture.Name);
    }

    [Fact]
    public async Task Save_WithoutChangingLanguage_NeverRecordsAPreference_OrSwitchesCulture()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());
        _settingsService.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var cut = Render<GeneralSettingsTab>();
        await cut.Find("form").SubmitAsync();

        _languagePreferenceStore.DidNotReceive().Set(Arg.Any<string>());
    }

    [Fact]
    public async Task NoRestartNotice_EverAppears_BeforeOrAfterAChangeOrASave()
    {
        using var _ = new CultureRestorer();
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());
        _settingsService.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var cut = Render<GeneralSettingsTab>();
        Assert.DoesNotContain("Restart", cut.Markup, StringComparison.OrdinalIgnoreCase);

        cut.Find("#languageCode").Change("fr-FR");
        Assert.DoesNotContain("Restart", cut.Markup, StringComparison.OrdinalIgnoreCase);

        await cut.Find("form").SubmitAsync();
        Assert.DoesNotContain("Restart", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }
}
