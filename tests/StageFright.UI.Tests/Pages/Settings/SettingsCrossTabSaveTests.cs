using Bunit;
using Microsoft.Extensions.DependencyInjection;
using StageFright.Core.Contracts;
using StageFright.UI.Pages.Settings;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Settings;

/// <summary>
/// Cross-tab concurrency tests (FR-119/SC-103): a stale in-memory copy in one tab must
/// never clobber a change already saved from the other tab during the same page visit.
/// Uses a stateful fake ISettingsService (rather than an NSubstitute mock) so GetAsync
/// reflects prior SaveAsync calls, faithfully simulating the shared underlying DB row.
/// </summary>
public class SettingsCrossTabSaveTests : BunitContext
{
    private sealed class FakeSettingsService : ISettingsService
    {
        private AppSettings? _stored;

        public FakeSettingsService(AppSettings initial) => _stored = Clone(initial);

        public Task<AppSettings?> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(_stored is null ? null : Clone(_stored));

        public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
        {
            _stored = Clone(settings);
            return Task.CompletedTask;
        }

        private static AppSettings Clone(AppSettings s) => new()
        {
            Id = s.Id,
            OrganizationName = s.OrganizationName,
            Abn = s.Abn,
            AnnualFee = s.AnnualFee,
            AttendanceFee = s.AttendanceFee,
            MembershipRenewalMonth = s.MembershipRenewalMonth,
            CommitteeRenewalMonth = s.CommitteeRenewalMonth,
            FinancialYearStartMonth = s.FinancialYearStartMonth,
            IsGstRegistered = s.IsGstRegistered,
            AnnualFeeGstCode = s.AnnualFeeGstCode,
            AttendanceFeeGstCode = s.AttendanceFeeGstCode,
            MaxAgeRangeYears = s.MaxAgeRangeYears,
            MinimumMemberAge = s.MinimumMemberAge,
            Theme = s.Theme,
            ShowParticipationGraphs = s.ShowParticipationGraphs,
            GeneralCommitteeSeatCountTarget = s.GeneralCommitteeSeatCountTarget,
            SchemaVersion = s.SchemaVersion,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        };
    }

    private static AppSettings MakeSettings() => new()
    {
        Id = Guid.NewGuid(),
        OrganizationName = "Test Org",
        Abn = "51824753556",
        AnnualFee = 75m,
        AttendanceFee = 5m,
        MembershipRenewalMonth = 1,
        IsGstRegistered = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GstToggleSavedFirst_SurvivesSubsequentGeneralTabSave_FromStaleCopy()
    {
        var fake = new FakeSettingsService(MakeSettings());
        Services.AddSingleton<ISettingsService>(fake);

        // General tab loads its own (soon-to-be-stale) copy and makes an unsaved edit.
        var generalCut = Render<GeneralSettingsTab>();
        generalCut.Find("#annualFee").Change("999.00");

        // GST/BAS tab loads independently, toggles registration on, confirms, and saves.
        var gstCut = Render<GstSettingsTab>();
        gstCut.Find("#gstRegistered").Click();
        gstCut.Find("#gst-toggle-confirm-btn").Click();
        await gstCut.Find("form").SubmitAsync();

        // General tab now saves its own (still-unrelated) change from its stale copy.
        await generalCut.Find("form").SubmitAsync();

        var current = await fake.GetAsync();
        Assert.True(current!.IsGstRegistered);
        Assert.Equal(999.00m, current.AnnualFee);
    }

    [Fact]
    public async Task AbnSavedFirst_SurvivesSubsequentGstTabSave_FromStaleCopy()
    {
        var fake = new FakeSettingsService(MakeSettings());
        Services.AddSingleton<ISettingsService>(fake);

        // GST/BAS tab loads its own (soon-to-be-stale) copy and stages an unsaved toggle.
        var gstCut = Render<GstSettingsTab>();
        gstCut.Find("#gstRegistered").Click();
        gstCut.Find("#gst-toggle-confirm-btn").Click();

        // General tab loads independently, changes the ABN, and saves.
        var generalCut = Render<GeneralSettingsTab>();
        generalCut.Find("#abn").Change("83914571673");
        await generalCut.Find("form").SubmitAsync();

        // GST/BAS tab now saves its staged toggle from its stale copy.
        await gstCut.Find("form").SubmitAsync();

        var current = await fake.GetAsync();
        Assert.Equal("83914571673", current!.Abn);
        Assert.True(current.IsGstRegistered);
    }
}
