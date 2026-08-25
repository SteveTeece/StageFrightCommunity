using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Agm;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Members;
using StageFright.Core.Modules.Settings;
using StageFright.Data;
using StageFright.Data.Repositories;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;
using StageFright.Reports.Registry;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V18: the AGM workflow (spec 013) — recording an AGM's attendance and
/// committee elections, committee reporting picking up the result (SC-002), a special election
/// mid-term, reviewing a past AGM, and archiving it. Uses a real SQLite in-memory database with
/// full EF migrations, matching the existing integration-test convention (no DI container).
/// </summary>
public sealed class V18_AgmWorkflowTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid PresidentMemberId = Guid.NewGuid();
    private static readonly Guid GeneralMemberId = Guid.NewGuid();
    private static readonly Guid ReplacementPresidentMemberId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        _db.Members.AddRange(
            MakeMember(PresidentMemberId, "Alice", "President"),
            MakeMember(GeneralMemberId, "Bob", "General"),
            MakeMember(ReplacementPresidentMemberId, "Carol", "Replacement"));

        await _db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task FullAgmWorkflow_RecordSpecialElectionReviewAndArchive_WorksEndToEnd()
    {
        var agmSvc = BuildAgmService();
        var presidentTypeId = await GetBuiltInOfficeHolderTypeIdAsync("President");

        // --- Schedule then record the AGM: attendance + President + one general committee member ---
        var agmDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var scheduled = await agmSvc.ScheduleAsync(new ScheduleAgmRequest(agmDate, "Annual sitting"), TestContext.Current.CancellationToken);
        var agm = await agmSvc.RecordAsync(scheduled.Id, new RecordAgmRequest(
            AttendedMemberIds: [PresidentMemberId, GeneralMemberId],
            AllActiveMemberIds: [PresidentMemberId, GeneralMemberId, ReplacementPresidentMemberId],
            OfficeHolderAssignments: new Dictionary<Guid, Guid> { [presidentTypeId] = PresidentMemberId },
            GeneralCommitteeMemberIds: [GeneralMemberId]), TestContext.Current.CancellationToken);

        Assert.Equal(agmDate, agm.Date);

        var attendanceRecords = await _db.AgmAttendanceRecords.Where(a => a.AnnualGeneralMeetingId == agm.Id).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, attendanceRecords.Count);
        Assert.Equal(2, attendanceRecords.Count(a => a.Attended));

        var term = await _db.CommitteeTerms.SingleAsync(t => t.StartedByAgmId == agm.Id, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(term.EndDate);
        Assert.Equal(2026, term.LabelYear);

        // --- Committee reporting picks up the result (SC-002) ---
        var reportProvider = new CommitteeReportProvider(new CommitteePositionRecordRepository(_db), new MemberRepository(_db));
        var filters = new ReportFilterValues();
        filters.Set("memberFilter", "Active Only");
        var report = await reportProvider.GenerateAsync(filters, TestContext.Current.CancellationToken);

        var section = Assert.Single(report.Sections, s => s.Heading == "2026");
        Assert.Contains(section.Rows, r => r.Cells[1] == "President" && r.Cells[2] == "President, Alice");
        Assert.Contains(section.Rows, r => r.Cells[1] == "General Committee Members" && r.Cells[2].Contains("Bob"));

        // --- Special election: replace the President mid-term ---
        var outgoingPresidentRecord = await _db.CommitteePositionRecords
            .SingleAsync(r => r.CommitteeTermId == term.Id && r.OfficeHolderTypeId == presidentTypeId && r.EndDate == null, cancellationToken: TestContext.Current.CancellationToken);
        var replacementDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var incoming = await agmSvc.RecordSpecialElectionAsync(new RecordSpecialElectionRequest(
            outgoingPresidentRecord.Id, ReplacementPresidentMemberId, replacementDate), TestContext.Current.CancellationToken);

        Assert.Equal(ReplacementPresidentMemberId, incoming.MemberId);
        Assert.Equal(replacementDate, incoming.StartDate);

        var refreshedOutgoing = await _db.CommitteePositionRecords.SingleAsync(r => r.Id == outgoingPresidentRecord.Id, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(replacementDate, refreshedOutgoing.EndDate);

        // Reporting now shows both holders with dates for the same slot (FR-029).
        var reportAfterElection = await reportProvider.GenerateAsync(filters, TestContext.Current.CancellationToken);
        var sectionAfterElection = Assert.Single(reportAfterElection.Sections, s => s.Heading == "2026");
        var presidentRow = sectionAfterElection.Rows.Single(r => r.Cells[1] == "President");
        Assert.Contains("Alice", presidentRow.Cells[2]);
        Assert.Contains("Carol", presidentRow.Cells[2]);
        Assert.Contains("present", presidentRow.Cells[2]);

        // --- Review the past AGM ---
        var pastAgms = await agmSvc.GetPastAsync(TestContext.Current.CancellationToken);
        Assert.Contains(pastAgms, a => a.Id == agm.Id);

        var reviewedAgm = await agmSvc.GetByIdAsync(agm.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(reviewedAgm);
        Assert.Equal("Annual sitting", reviewedAgm!.Notes);

        // --- Archive it ---
        await agmSvc.ArchiveAsync(agm.Id, "coordinator", TestContext.Current.CancellationToken);

        var pastAgmsAfterArchive = await agmSvc.GetPastAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(pastAgmsAfterArchive, a => a.Id == agm.Id);

        var archivedAttendance = await _db.AgmAttendanceRecords
            .IgnoreQueryFilters()
            .Where(a => a.AnnualGeneralMeetingId == agm.Id)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.All(archivedAttendance, a => Assert.True(a.IsDeleted));
    }

    // --- AGM Attendance Sheet (spec 018) ---

    [Fact]
    public async Task AgmAttendanceSheet_GenerateAsync_Throws_ForUnknownAgm()
    {
        var svc = BuildAgmAttendanceSheetService();

        await Assert.ThrowsAsync<StageFright.Core.Exceptions.EntityNotFoundException>(
            () => svc.GenerateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AgmAttendanceSheet_GenerateAsync_Returns_PersistedRoster_SortedBySurname_WithAttendedStatus()
    {
        var agmSvc = BuildAgmService();
        var presidentTypeId = await GetBuiltInOfficeHolderTypeIdAsync("President");

        var scheduled = await agmSvc.ScheduleAsync(new ScheduleAgmRequest(
            new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), "Annual sitting"), TestContext.Current.CancellationToken);
        var agm = await agmSvc.RecordAsync(scheduled.Id, new RecordAgmRequest(
            AttendedMemberIds: [PresidentMemberId],
            AllActiveMemberIds: [PresidentMemberId, GeneralMemberId, ReplacementPresidentMemberId],
            OfficeHolderAssignments: new Dictionary<Guid, Guid> { [presidentTypeId] = PresidentMemberId },
            GeneralCommitteeMemberIds: [GeneralMemberId]), TestContext.Current.CancellationToken);

        var svc = BuildAgmAttendanceSheetService();
        var result = await svc.GenerateAsync(agm.Id, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Members.Count);
        Assert.Equal(agm.Date, result.AgmDate);
        Assert.True(string.CompareOrdinal(result.Members[0].LastName, result.Members[1].LastName) <= 0);
        Assert.True(string.CompareOrdinal(result.Members[1].LastName, result.Members[2].LastName) <= 0);
        Assert.Contains(result.Members, m => m.FirstName == "Alice" && m.Attended);
        Assert.Contains(result.Members, m => m.FirstName == "Bob" && !m.Attended);
    }

    [Fact]
    public async Task AgmAttendanceSheet_GenerateAsync_ReturnsEmptyList_ForAgmWithZeroAttendanceRecords()
    {
        var agmSvc = BuildAgmService();

        var scheduled = await agmSvc.ScheduleAsync(new ScheduleAgmRequest(
            new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), "Empty sitting"), TestContext.Current.CancellationToken);
        var agm = await agmSvc.RecordAsync(scheduled.Id, new RecordAgmRequest(
            AttendedMemberIds: [],
            AllActiveMemberIds: [],
            OfficeHolderAssignments: new Dictionary<Guid, Guid>(),
            GeneralCommitteeMemberIds: []), TestContext.Current.CancellationToken);

        var svc = BuildAgmAttendanceSheetService();
        var result = await svc.GenerateAsync(agm.Id, TestContext.Current.CancellationToken);

        Assert.Empty(result.Members);
    }

    // --- Helpers ---

    private StageFright.Core.Modules.Agm.AgmAttendanceSheetService BuildAgmAttendanceSheetService() =>
        new(new AgmRepository(_db), new AgmAttendanceRepository(_db), new MemberRepository(_db));

    private AgmService BuildAgmService()
    {
        var settingsRepo = new SettingsRepository(_db);
        var auditSvc = BuildAuditService();
        var settingsSvc = new SettingsService(settingsRepo, auditSvc);

        return new AgmService(
            new AgmRepository(_db),
            new AgmAttendanceRepository(_db),
            new CommitteeTermRepository(_db),
            new CommitteePositionRecordRepository(_db),
            settingsSvc,
            auditSvc,
            new UnitOfWork(_db));
    }

    private async Task<Guid> GetBuiltInOfficeHolderTypeIdAsync(string name)
    {
        var type = await _db.CommitteeOfficeHolderTypes.SingleAsync(t => t.Name == name);
        return type.Id;
    }

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
