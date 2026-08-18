using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.UI.Pages.Settings;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Settings;

/// <summary>
/// bUnit tests for GeneralSettingsTab after the ABN/GST removal (spec 016): the ABN field
/// and GST controls are both gone, and HandleSaveAsync merges tax-owned fields from a
/// fresh fetch before saving (FR-008/cross-tab save safety).
/// </summary>
public class GeneralSettingsTabTests : BunitContext
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    public GeneralSettingsTabTests()
    {
        Services.AddSingleton(_settingsService);
    }

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
                s.IsTaxApplicable &&
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
            Arg.Is<AppSettings>(s => s.AuditRetentionYears == 7),
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
}
