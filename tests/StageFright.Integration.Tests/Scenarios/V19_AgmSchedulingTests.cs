using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Agm;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Settings;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V19: scheduling future AGMs (spec 019) — schedule ahead, print a blank
/// attendance report before recording, record it, print the recorded roster, reject a second
/// same-year AGM, and confirm archiving frees the calendar year for a replacement. Uses a real
/// SQLite in-memory database with full EF migrations, matching V18's existing convention (no DI
/// container).
/// </summary>
public sealed class V19_AgmSchedulingTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid ActiveMemberOneId = Guid.NewGuid();
    private static readonly Guid ActiveMemberTwoId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        _db.Members.AddRange(
            MakeMember(ActiveMemberOneId, "Alice", "Anderson"),
            MakeMember(ActiveMemberTwoId, "Bob", "Baker"));

        await _db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task FullSchedulingWorkflow_ScheduleBlankPrintRecordReprint_RejectDuplicateYear_ArchiveFreesYear()
    {
        var agmSvc = BuildAgmService();
        var sheetSvc = BuildAgmAttendanceSheetService();

        // --- Schedule an AGM: date + notes only, no attendance/elections/term ---
        var agmDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var scheduled = await agmSvc.ScheduleAsync(
            new ScheduleAgmRequest(agmDate, "Annual sitting"), TestContext.Current.CancellationToken);

        Assert.False(scheduled.IsRecorded);
        Assert.Equal(0, await _db.AgmAttendanceRecords.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, await _db.CommitteePositionRecords.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, await _db.CommitteeTerms.CountAsync(cancellationToken: TestContext.Current.CancellationToken));

        // --- Print its blank report: every active member, unchecked ---
        var blankReport = await sheetSvc.GenerateAsync(scheduled.Id, TestContext.Current.CancellationToken);

        Assert.Equal(2, blankReport.Members.Count);
        Assert.All(blankReport.Members, m => Assert.False(m.Attended));
        Assert.Contains(blankReport.Members, m => m.FirstName == "Alice");
        Assert.Contains(blankReport.Members, m => m.FirstName == "Bob");

        // --- Record it ---
        var recorded = await agmSvc.RecordAsync(scheduled.Id, new RecordAgmRequest(
            AttendedMemberIds: [ActiveMemberOneId],
            AllActiveMemberIds: [ActiveMemberOneId, ActiveMemberTwoId],
            OfficeHolderAssignments: new Dictionary<Guid, Guid>(),
            GeneralCommitteeMemberIds: []), TestContext.Current.CancellationToken);

        Assert.True(recorded.IsRecorded);

        // --- Reprint: now the fixed recorded roster with real attendance ---
        var recordedReport = await sheetSvc.GenerateAsync(scheduled.Id, TestContext.Current.CancellationToken);

        Assert.Equal(2, recordedReport.Members.Count);
        Assert.Contains(recordedReport.Members, m => m.FirstName == "Alice" && m.Attended);
        Assert.Contains(recordedReport.Members, m => m.FirstName == "Bob" && !m.Attended);

        // --- A second AGM in the same calendar year is rejected; nothing persisted ---
        var duplicateEx = await Assert.ThrowsAsync<ValidationException>(() =>
            agmSvc.ScheduleAsync(new ScheduleAgmRequest(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), null), TestContext.Current.CancellationToken));
        Assert.NotNull(duplicateEx);
        Assert.Equal(1, await _db.AnnualGeneralMeetings.CountAsync(cancellationToken: TestContext.Current.CancellationToken));

        // --- Archive the recorded AGM: frees its calendar year ---
        await agmSvc.ArchiveAsync(scheduled.Id, "coordinator", TestContext.Current.CancellationToken);

        // --- A replacement AGM can now be scheduled for the same freed year ---
        var replacement = await agmSvc.ScheduleAsync(
            new ScheduleAgmRequest(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), "Replacement AGM"),
            TestContext.Current.CancellationToken);

        Assert.False(replacement.IsRecorded);
        Assert.NotEqual(scheduled.Id, replacement.Id);
    }

    // --- Helpers ---

    private AgmService BuildAgmService()
    {
        var settingsRepo = new SettingsRepository(_db);
        var auditSvc = BuildAuditService();
        var settingsSvc = new SettingsService(settingsRepo, auditSvc, RealLocalizer.Instance);

        return new AgmService(
            new AgmRepository(_db),
            new AgmAttendanceRepository(_db),
            new CommitteeTermRepository(_db),
            new CommitteePositionRecordRepository(_db),
            settingsSvc,
            auditSvc,
            new UnitOfWork(_db),
            RealLocalizer.Instance);
    }

    private AgmAttendanceSheetService BuildAgmAttendanceSheetService() =>
        new(new AgmRepository(_db), new AgmAttendanceRepository(_db), new MemberRepository(_db));

    private static AuditTrailService BuildAuditService()
    {
        var auditRepo = NSubstitute.Substitute.For<StageFright.Core.Contracts.IAuditTrailRepository>();
        return new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
    }

    private static Member MakeMember(Guid id, string firstName, string lastName) => new()
    {
        Id = id, FirstName = firstName, LastName = lastName, StreetAddress = "1 Test St",
        JoinDate = DateTime.UtcNow.AddYears(-1), Status = MemberStatus.Active,
        ActivateDate = DateTime.UtcNow.Date, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
