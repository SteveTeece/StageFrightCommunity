using Bunit;
using Microsoft.Extensions.DependencyInjection;
using StageFright.Core.Contracts;
using StageFright.UI.Pages.Settings;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Settings;

/// <summary>
/// Cross-tab concurrency tests (FR-008/SC-103): a stale in-memory copy in one tab must
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
            AnnualFee = s.AnnualFee,
            AttendanceFee = s.AttendanceFee,
            MembershipRenewalMonth = s.MembershipRenewalMonth,
            CommitteeRenewalMonth = s.CommitteeRenewalMonth,
            FinancialYearStartMonth = s.FinancialYearStartMonth,
            IsTaxApplicable = s.IsTaxApplicable,
            TaxRate = s.TaxRate,
            AnnualFeeTaxCode = s.AnnualFeeTaxCode,
            AttendanceFeeTaxCode = s.AttendanceFeeTaxCode,
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
        AnnualFee = 75m,
        AttendanceFee = 5m,
        MembershipRenewalMonth = 1,
        IsTaxApplicable = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task TaxToggleSavedFirst_SurvivesSubsequentGeneralTabSave_FromStaleCopy()
    {
        var fake = new FakeSettingsService(MakeSettings());
        Services.AddSingleton<ISettingsService>(fake);

        // General tab loads its own (soon-to-be-stale) copy and makes an unsaved edit.
        var generalCut = Render<GeneralSettingsTab>();
        generalCut.Find("#annualFee").Change("999.00");

        // Sales Tax tab loads independently, toggles applicability on, confirms, and saves.
        var taxCut = Render<TaxSettingsTab>();
        taxCut.Find("#taxApplicable").Click();
        taxCut.Find("#tax-toggle-confirm-btn").Click();
        taxCut.Find("#taxRate").Change("10");
        await taxCut.Find("form").SubmitAsync();

        // General tab now saves its own (still-unrelated) change from its stale copy.
        await generalCut.Find("form").SubmitAsync();

        var current = await fake.GetAsync();
        Assert.True(current!.IsTaxApplicable);
        Assert.Equal(999.00m, current.AnnualFee);
    }

    [Fact]
    public async Task OrganizationNameSavedFirst_SurvivesSubsequentTaxTabSave_FromStaleCopy()
    {
        var fake = new FakeSettingsService(MakeSettings());
        Services.AddSingleton<ISettingsService>(fake);

        // Sales Tax tab loads its own (soon-to-be-stale) copy and stages an unsaved toggle.
        var taxCut = Render<TaxSettingsTab>();
        taxCut.Find("#taxApplicable").Click();
        taxCut.Find("#tax-toggle-confirm-btn").Click();
        taxCut.Find("#taxRate").Change("10");

        // General tab loads independently, changes the organisation name, and saves.
        var generalCut = Render<GeneralSettingsTab>();
        generalCut.Find("#orgName").Change("Renamed Org");
        await generalCut.Find("form").SubmitAsync();

        // Sales Tax tab now saves its staged toggle from its stale copy.
        await taxCut.Find("form").SubmitAsync();

        var current = await fake.GetAsync();
        Assert.Equal("Renamed Org", current!.OrganizationName);
        Assert.True(current.IsTaxApplicable);
    }
}
