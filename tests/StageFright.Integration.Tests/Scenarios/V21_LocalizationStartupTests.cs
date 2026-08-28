using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Localization;
using StageFright.Core.Modules.Settings;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for spec 027 US3 — the app opens in the resolved language and a change is
/// applied on the next launch. Uses the real <see cref="SettingsService"/>,
/// <see cref="SupportedLanguagesCatalog"/> and <see cref="LanguageProvider"/> against a real
/// SQLite database: startup honours a persisted <c>Settings.LanguageCode</c> (SC-005) and
/// resolves to <c>en-AU</c> when the OS language ships no matching set (SC-010); changing the
/// language leaves every stored value and GL balance byte-identical (SC-006 / FR-016).
/// </summary>
public sealed class V21_LocalizationStartupTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;
    private readonly ISystemCultureProvider _systemCulture = Substitute.For<ISystemCultureProvider>();

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Should_ResolveToThePersistedLanguage_When_SettingsHasAnExplicitChoice_Integration()
    {
        await SeedSettingsAsync(languageCode: "en-AU");
        _systemCulture.GetUiCulture().Returns(CultureInfo.GetCultureInfo("de-DE")); // unrelated OS language

        var culture = await BuildLanguageProvider().ResolveStartupCultureAsync(TestContext.Current.CancellationToken);

        Assert.Equal("en-AU", culture.Name);
    }

    [Fact]
    public async Task Should_ResolveToEnAu_When_NoExplicitChoiceAndTheOsLanguageShipsNoSet_Integration()
    {
        await SeedSettingsAsync(languageCode: null);
        _systemCulture.GetUiCulture().Returns(CultureInfo.GetCultureInfo("de-DE"));

        var culture = await BuildLanguageProvider().ResolveStartupCultureAsync(TestContext.Current.CancellationToken);

        Assert.Equal("en-AU", culture.Name);
    }

    [Fact]
    public async Task Should_ResolveToEnAu_When_NoExplicitChoiceAndTheOsIsARegionalEnglish_Integration()
    {
        await SeedSettingsAsync(languageCode: null);
        _systemCulture.GetUiCulture().Returns(CultureInfo.GetCultureInfo("en-GB"));

        var culture = await BuildLanguageProvider().ResolveStartupCultureAsync(TestContext.Current.CancellationToken);

        Assert.Equal("en-AU", culture.Name); // parent-language ("en") match against the only shipped set
    }

    [Fact]
    public async Task Should_LeaveEveryStoredValueAndGlBalanceUnchanged_When_TheLanguageIsChanged_Integration()
    {
        await SeedSettingsAsync(languageCode: null);
        var member = new Member
        {
            Id = Guid.NewGuid(), FirstName = "Pat", StreetAddress = "1 Test St",
            Status = MemberStatus.Active,
            JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var before = await SnapshotAsync();

        var settingsService = BuildSettingsService();
        var settings = await settingsService.GetAsync(TestContext.Current.CancellationToken);
        settings!.LanguageCode = "en-AU";
        await settingsService.SaveAsync(settings, TestContext.Current.CancellationToken);

        _db.ChangeTracker.Clear();
        var after = await SnapshotAsync();

        Assert.Equal(before.MemberJson, after.MemberJson);
        Assert.Equal(before.RowCounts, after.RowCounts);
        Assert.Equal(before.GlTransactionSum, after.GlTransactionSum);

        var reloaded = await settingsService.GetAsync(TestContext.Current.CancellationToken);
        Assert.Equal("en-AU", reloaded!.LanguageCode);
    }

    // --- helpers -------------------------------------------------------------------

    private LanguageProvider BuildLanguageProvider() =>
        new(BuildSettingsService(), new SupportedLanguagesCatalog(), _systemCulture);

    private SettingsService BuildSettingsService()
    {
        var auditSvc = new AuditTrailService(new AuditTrailRepository(_db), NullLogger<AuditTrailService>.Instance);
        return new SettingsService(new SettingsRepository(_db), auditSvc, RealLocalizer.Instance);
    }

    private async Task SeedSettingsAsync(string? languageCode)
    {
        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(),
            OrganizationName = "Test Choir",
            AnnualFee = 60m,
            AttendanceFee = 5m,
            MembershipRenewalMonth = 1,
            CommitteeRenewalMonth = 1,
            FinancialYearStartMonth = 7,
            MaxAgeRangeYears = 150,
            MinimumMemberAge = 0,
            Theme = Theme.Dark,
            LanguageCode = languageCode,
            SchemaVersion = "1.1.0",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();
    }

    private async Task<(string MemberJson, string RowCounts, decimal GlTransactionSum)> SnapshotAsync()
    {
        var member = await _db.Members.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        var counts = new Dictionary<string, int>
        {
            ["Members"] = await _db.Members.CountAsync(TestContext.Current.CancellationToken),
            ["Accounts"] = await _db.Accounts.CountAsync(TestContext.Current.CancellationToken),
            ["Transactions"] = await _db.Transactions.CountAsync(TestContext.Current.CancellationToken),
            ["JournalEntries"] = await _db.JournalEntries.CountAsync(TestContext.Current.CancellationToken),
            ["Fees"] = await _db.Fees.CountAsync(TestContext.Current.CancellationToken),
            ["Payments"] = await _db.Payments.CountAsync(TestContext.Current.CancellationToken),
        };
        var glSum = await _db.Transactions.AsNoTracking()
            .SumAsync(t => (decimal?)(t.DebitAmount - t.CreditAmount), TestContext.Current.CancellationToken) ?? 0m;
        return (JsonSerializer.Serialize(member), JsonSerializer.Serialize(counts), glSum);
    }
}
