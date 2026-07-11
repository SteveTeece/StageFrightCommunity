using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.UI.Pages.Settings;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Settings;

/// <summary>
/// bUnit tests for GstSettingsTab: the GST toggle, its confirm-dialog gating, GST-code
/// dropdown visibility, and HandleSaveAsync's cross-tab merge of non-GST-owned fields
/// (FR-117/FR-119) — moved here from what was GeneralSettingsTab's GST section.
/// </summary>
public class GstSettingsTabTests : BunitContext
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    public GstSettingsTabTests()
    {
        Services.AddSingleton(_settingsService);
    }

    private static AppSettings MakeSettings(bool gstRegistered = false) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationName = "Test Org",
        Abn = "51824753556",
        AnnualFee = 75m,
        AttendanceFee = 5m,
        MembershipRenewalMonth = 1,
        IsGstRegistered = gstRegistered,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public void Renders_GstToggle_Unchecked_ByDefault()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<GstSettingsTab>();

        var checkbox = cut.Find("#gstRegistered");
        Assert.Null(checkbox.GetAttribute("checked"));
    }

    [Fact]
    public void TogglingOn_ShowsConfirmDialog_WithoutCommittingImmediately()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<GstSettingsTab>();
        cut.Find("#gstRegistered").Change(true);

        var confirm = cut.Find("#gst-toggle-confirm");
        Assert.Contains("future income and expense postings will split out GST", confirm.TextContent);
        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#annualFeeGstCode"));
    }

    [Fact]
    public void TogglingOff_ShowsDeregistrationWarning()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings(gstRegistered: true));

        var cut = Render<GstSettingsTab>();
        cut.Find("#gstRegistered").Change(false);

        var confirm = cut.Find("#gst-toggle-confirm");
        Assert.Contains("GST fields will be hidden", confirm.TextContent);
    }

    [Fact]
    public void ConfirmGstToggle_CommitsChange_AndShowsDropdowns()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<GstSettingsTab>();
        cut.Find("#gstRegistered").Change(true);
        cut.Find("#gst-toggle-confirm-btn").Click();

        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#gst-toggle-confirm"));
        cut.Find("#annualFeeGstCode");
        cut.Find("#attendanceFeeGstCode");
    }

    [Fact]
    public void CancelGstToggle_DiscardsChange_AndHidesDialog()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<GstSettingsTab>();
        cut.Find("#gstRegistered").Change(true);
        cut.Find("#gst-toggle-confirm .btn-outline-secondary").Click();

        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#gst-toggle-confirm"));
        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#annualFeeGstCode"));
        Assert.Null(cut.Find("#gstRegistered").GetAttribute("checked"));
    }

    [Fact]
    public void GstCodeDropdowns_HiddenWhenNotRegistered()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());

        var cut = Render<GstSettingsTab>();

        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#annualFeeGstCode"));
        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find("#attendanceFeeGstCode"));
    }

    [Fact]
    public async Task HandleSaveAsync_PersistsGstFields()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(MakeSettings());
        _settingsService.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var cut = Render<GstSettingsTab>();
        cut.Find("#gstRegistered").Change(true);
        cut.Find("#gst-toggle-confirm-btn").Click();
        cut.Find("#annualFeeGstCode").Change("Gst");

        await cut.Find("form").SubmitAsync();

        await _settingsService.Received(1).SaveAsync(
            Arg.Is<AppSettings>(s => s.IsGstRegistered && s.AnnualFeeGstCode == GstCode.Gst),
            Arg.Any<CancellationToken>());
        Assert.Contains("Settings saved successfully", cut.Markup);
    }

    [Fact]
    public async Task HandleSaveAsync_MergesNonGstFields_FromFreshFetch()
    {
        var loaded = MakeSettings();
        var freshFromDb = MakeSettings();
        freshFromDb.OrganizationName = "Renamed By General Tab";
        freshFromDb.Abn = "83914571673";

        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(loaded, freshFromDb);
        _settingsService.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var cut = Render<GstSettingsTab>();
        cut.Find("#gstRegistered").Change(true);
        cut.Find("#gst-toggle-confirm-btn").Click();

        await cut.Find("form").SubmitAsync();

        await _settingsService.Received(1).SaveAsync(
            Arg.Is<AppSettings>(s =>
                s.IsGstRegistered &&
                s.OrganizationName == "Renamed By General Tab" &&
                s.Abn == "83914571673"),
            Arg.Any<CancellationToken>());
    }
}
