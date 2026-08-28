using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.UI.Pages.Settings;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Settings;

/// <summary>
/// bUnit tests for TaxSettingsTab: the tax-applicable toggle, its confirm-dialog gating,
/// tax-code dropdown visibility, and HandleSaveAsync's cross-tab merge of non-tax-owned
/// fields (FR-117/FR-119) — moved here from what was GeneralSettingsTab's GST section.
/// </summary>
public class TaxSettingsTabTests : LocalizedTestContext
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    public TaxSettingsTabTests()
    {
        Services.AddSingleton(_settingsService);
    }

    private static AppSettings MakeSettings(bool taxApplicable = false) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationName = "Test Org",
        AnnualFee = 75m,
        AttendanceFee = 5m,
        MembershipRenewalMonth = 1,
        IsTaxApplicable = taxApplicable,
        TaxRate = taxApplicable ? 10m : null,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public void Renders_TaxToggle_Unchecked_ByDefault()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<TaxSettingsTab>();

        var toggle = cut.Find("#taxApplicable");
        Assert.Equal("false", toggle.GetAttribute("aria-checked"));
    }

    [Fact]
    public void TogglingOn_ShowsConfirmDialog_WithoutCommittingImmediately()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<TaxSettingsTab>();
        cut.Find("#taxApplicable").Click();

        var confirm = cut.Find("#tax-toggle-confirm");
        Assert.Contains("future income and expense postings will split out sales tax", confirm.TextContent);
        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#annualFeeTaxCode"));
    }

    [Fact]
    public void TogglingOff_ShowsDeregistrationWarning()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings(taxApplicable: true));

        var cut = Render<TaxSettingsTab>();
        cut.Find("#taxApplicable").Click();

        var confirm = cut.Find("#tax-toggle-confirm");
        Assert.Contains("tax fields will be hidden", confirm.TextContent);
    }

    [Fact]
    public void ConfirmTaxToggle_CommitsChange_AndShowsDropdowns()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<TaxSettingsTab>();
        cut.Find("#taxApplicable").Click();
        cut.Find("#tax-toggle-confirm-btn").Click();

        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#tax-toggle-confirm"));
        cut.Find("#annualFeeTaxCode");
        cut.Find("#attendanceFeeTaxCode");
    }

    [Fact]
    public void CancelTaxToggle_DiscardsChange_AndHidesDialog()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<TaxSettingsTab>();
        cut.Find("#taxApplicable").Click();
        cut.Find("#tax-toggle-confirm .btn-outline-secondary").Click();

        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#tax-toggle-confirm"));
        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#annualFeeTaxCode"));
        Assert.Equal("false", cut.Find("#taxApplicable").GetAttribute("aria-checked"));
    }

    [Fact]
    public void TaxCodeDropdowns_HiddenWhenNotApplicable()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<TaxSettingsTab>();

        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#annualFeeTaxCode"));
        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#attendanceFeeTaxCode"));
    }

    [Fact]
    public async Task HandleSaveAsync_PersistsTaxFields()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());
        _settingsService.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var cut = Render<TaxSettingsTab>();
        cut.Find("#taxApplicable").Click();
        cut.Find("#tax-toggle-confirm-btn").Click();
        cut.Find("#taxRate").Change("10");
        cut.Find("#annualFeeTaxCode").Change("Taxable");

        await cut.Find("form").SubmitAsync();

        await _settingsService.Received(1).SaveAsync(
            Arg.Is<AppSettings>(s => s!.IsTaxApplicable && s.AnnualFeeTaxCode == TaxCode.Taxable),
            Arg.Any<CancellationToken>());
        Assert.Contains("Settings saved successfully", cut.Markup);
    }

    [Fact]
    public async Task HandleSaveAsync_MergesNonTaxFields_FromFreshFetch()
    {
        var loaded = MakeSettings();
        var freshFromDb = MakeSettings();
        freshFromDb.OrganizationName = "Renamed By General Tab";

        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(loaded, freshFromDb);
        _settingsService.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var cut = Render<TaxSettingsTab>();
        cut.Find("#taxApplicable").Click();
        cut.Find("#tax-toggle-confirm-btn").Click();
        cut.Find("#taxRate").Change("10");

        await cut.Find("form").SubmitAsync();

        await _settingsService.Received(1).SaveAsync(
            Arg.Is<AppSettings>(s =>
                s!.IsTaxApplicable &&
                s.OrganizationName == "Renamed By General Tab"),
            Arg.Any<CancellationToken>());
    }
}
