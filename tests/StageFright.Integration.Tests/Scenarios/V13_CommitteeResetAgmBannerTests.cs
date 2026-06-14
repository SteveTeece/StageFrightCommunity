using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Members;
using StageFright.Core.Modules.Settings;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V13 — Committee Annual Reset and AGM Banner (FR-031).
/// Covers: AGM event recorded → banner shown; reset clears current year, preserves history;
/// LastCommitteeResetYear updated; audit entry written; banner disappears after reset.
/// </summary>
public sealed class V13_CommitteeResetAgmBannerTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;
    private readonly int _currentYear = DateTime.UtcNow.Year;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- AGM banner conditions ---

    [Fact]
    public async Task CheckAgmBanner_ReturnsNull_WhenNoAgmRecorded()
    {
        await SeedSettingsAsync(lastResetYear: null);

        var svc = BuildResetService();
        var banner = await svc.CheckAgmBannerAsync();

        Assert.Null(banner);
    }

    [Fact]
    public async Task CheckAgmBanner_ReturnsNull_WhenAgmWithinLast7Days()
    {
        await SeedSettingsAsync(lastResetYear: null);
        await SeedAgmEventAsync(daysAgo: 3);

        var svc = BuildResetService();
        var banner = await svc.CheckAgmBannerAsync();

        Assert.Null(banner);
    }

    [Fact]
    public async Task CheckAgmBanner_ReturnsBannerMessage_WhenAgmOlderThan7Days_AndNotYetReset()
    {
        await SeedSettingsAsync(lastResetYear: _currentYear - 1);
        await SeedAgmEventAsync(daysAgo: 10);

        var svc = BuildResetService();
        var banner = await svc.CheckAgmBannerAsync();

        Assert.NotNull(banner);
        Assert.NotEmpty(banner);
    }

    [Fact]
    public async Task CheckAgmBanner_ReturnsNull_WhenAlreadyResetThisYear()
    {
        await SeedSettingsAsync(lastResetYear: _currentYear);
        await SeedAgmEventAsync(daysAgo: 10);

        var svc = BuildResetService();
        var banner = await svc.CheckAgmBannerAsync();

        Assert.Null(banner);
    }

    // --- Reset behaviour ---

    [Fact]
    public async Task ResetAsync_SoftDeletesCurrentYearCommittee_PreservesHistory()
    {
        await SeedSettingsAsync(lastResetYear: null);

        var memberId = Guid.NewGuid();
        var member = new Member
        {
            Id = memberId, Name = "Test Member", StreetAddress = "1 St",
            JoinDate = DateTime.UtcNow.AddYears(-2), Status = MemberStatus.Active,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        await _db.Members.AddAsync(member);

        // Historical record from last year
        var priorYear = new CommitteeMembership
        {
            Id = Guid.NewGuid(), MemberId = memberId,
            Year = _currentYear - 1, Position = "Secretary",
            CreatedAt = DateTime.UtcNow.AddYears(-1), UpdatedAt = DateTime.UtcNow.AddYears(-1)
        };
        // Current-year record
        var thisYear = new CommitteeMembership
        {
            Id = Guid.NewGuid(), MemberId = memberId,
            Year = _currentYear, Position = "President",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        await _db.CommitteeMemberships.AddRangeAsync(priorYear, thisYear);
        await _db.SaveChangesAsync();

        var svc = BuildResetService();
        await svc.ResetAsync();

        // Current-year record should be soft-deleted
        var currentYearRecord = await _db.CommitteeMemberships
            .IgnoreQueryFilters()
            .SingleAsync(r => r.Id == thisYear.Id);
        Assert.True(currentYearRecord.IsDeleted);

        // Prior-year record should be untouched
        var priorYearRecord = await _db.CommitteeMemberships
            .IgnoreQueryFilters()
            .SingleAsync(r => r.Id == priorYear.Id);
        Assert.False(priorYearRecord.IsDeleted);
    }

    [Fact]
    public async Task ResetAsync_UpdatesLastCommitteeResetYear_InSettings()
    {
        await SeedSettingsAsync(lastResetYear: null);

        var svc = BuildResetService();
        await svc.ResetAsync();

        var updated = await new SettingsRepository(_db).GetAsync();
        Assert.Equal(_currentYear, updated!.LastCommitteeResetYear);
    }

    [Fact]
    public async Task ResetAsync_WritesAuditEntry()
    {
        await SeedSettingsAsync(lastResetYear: null);

        var svc = BuildResetService();
        await svc.ResetAsync();

        var auditEntries = await _db.AuditTrailEntries.ToListAsync();
        Assert.Contains(auditEntries, e =>
            e.Action == AuditAction.CommitteeReset &&
            e.NewValue == _currentYear.ToString());
    }

    [Fact]
    public async Task AfterReset_BannerNoLongerShown()
    {
        await SeedSettingsAsync(lastResetYear: _currentYear - 1);
        await SeedAgmEventAsync(daysAgo: 10);

        var svc = BuildResetService();

        var bannerBefore = await svc.CheckAgmBannerAsync();
        Assert.NotNull(bannerBefore);

        await svc.ResetAsync();

        var bannerAfter = await svc.CheckAgmBannerAsync();
        Assert.Null(bannerAfter);
    }

    // --- Helpers ---

    private async Task<Core.Entities.Settings> SeedSettingsAsync(int? lastResetYear)
    {
        var settings = new Core.Entities.Settings
        {
            Id = Guid.NewGuid(),
            OrganizationName = "Test Org",
            AnnualFee = 50m,
            AttendanceFee = 5m,
            MembershipRenewalMonth = 1,
            LastCommitteeResetYear = lastResetYear,
            SchemaVersion = "1.0.0",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _db.Settings.AddAsync(settings);

        // Seed required system event type for AGM events
        var agmType = new EventType
        {
            Id = Guid.NewGuid(), Name = "Annual General Meeting",
            IsSystemDefault = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        await _db.EventTypes.AddAsync(agmType);
        await _db.SaveChangesAsync();
        return settings;
    }

    private async Task SeedAgmEventAsync(int daysAgo)
    {
        var agmType = await _db.EventTypes.SingleAsync(t => t.Name == "Annual General Meeting");
        var ev = new Event
        {
            Id = Guid.NewGuid(),
            Date = DateTime.UtcNow.AddDays(-daysAgo),
            EventTypeId = agmType.Id,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        await _db.Events.AddAsync(ev);
        await _db.SaveChangesAsync();
    }

    private CommitteeAnnualResetService BuildResetService()
    {
        var committeeRepo = new CommitteeMembershipRepository(_db);
        var settingsRepo = new SettingsRepository(_db);
        var eventRepo = new EventRepository(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditService = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var settingsService = new SettingsService(settingsRepo, auditService);
        var unitOfWork = new UnitOfWork(_db);
        return new CommitteeAnnualResetService(committeeRepo, settingsService, eventRepo, auditService, unitOfWork);
    }
}
