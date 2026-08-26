using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Agm;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Agm;

/// <summary>
/// Unit tests for AgmService — scheduling, atomic AGM recording against a previously scheduled
/// AGM, committee-term chaining, archiving, and special elections.
/// </summary>
public class AgmServiceTests : TestBase
{
    private readonly IAgmRepository _agmRepo = Substitute.For<IAgmRepository>();
    private readonly IAgmAttendanceRepository _attendanceRepo = Substitute.For<IAgmAttendanceRepository>();
    private readonly ICommitteeTermRepository _termRepo = Substitute.For<ICommitteeTermRepository>();
    private readonly ICommitteePositionRecordRepository _positionRepo = Substitute.For<ICommitteePositionRecordRepository>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public AgmServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _agmRepo.AddAsync(Arg.Any<AnnualGeneralMeeting>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<AnnualGeneralMeeting>(0));
        _agmRepo.ExistsForYearAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(false);
        _termRepo.AddAsync(Arg.Any<CommitteeTerm>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<CommitteeTerm>(0));
        _positionRepo.AddAsync(Arg.Any<CommitteePositionRecord>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<CommitteePositionRecord>(0));

        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new Settings
            {
                Id = Guid.NewGuid(), OrganizationName = "Test", AnnualFee = 50m, AttendanceFee = 5m,
                MembershipRenewalMonth = 1, GeneralCommitteeSeatCountTarget = 5,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
    }

    private AgmService CreateService() =>
        new(_agmRepo, _attendanceRepo, _termRepo, _positionRepo, _settingsService, _audit, _unitOfWork);

    private static RecordAgmRequest MakeRequest(
        IReadOnlyList<Guid>? attended = null,
        IReadOnlyList<Guid>? allActive = null,
        IReadOnlyDictionary<Guid, Guid>? officeHolders = null,
        IReadOnlyList<Guid>? generalCommittee = null) => new(
            attended ?? [],
            allActive ?? [],
            officeHolders ?? new Dictionary<Guid, Guid>(),
            generalCommittee ?? []);

    /// <summary>Seeds a scheduled (not-yet-recorded) AGM that GetByIdAsync will return.</summary>
    private AnnualGeneralMeeting SeedScheduledAgm(DateTime? date = null, Guid? id = null)
    {
        var agm = new AnnualGeneralMeeting
        {
            Id = id ?? Guid.NewGuid(),
            Date = date ?? new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            Notes = "Annual sitting",
            IsRecorded = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _agmRepo.GetByIdAsync(agm.Id, Arg.Any<CancellationToken>()).Returns(agm);
        return agm;
    }

    // --- ScheduleAsync ---

    [Fact]
    public async Task ScheduleAsync_PersistsDateNotesAndIsRecordedFalse()
    {
        var svc = CreateService();
        var date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);

        var result = await svc.ScheduleAsync(new ScheduleAgmRequest(date, "Annual sitting"), Ct);

        Assert.Equal(date, result.Date);
        Assert.Equal("Annual sitting", result.Notes);
        Assert.False(result.IsRecorded);
    }

    [Fact]
    public async Task ScheduleAsync_CreatesNoAttendanceElectionsOrTerm()
    {
        var svc = CreateService();
        await svc.ScheduleAsync(new ScheduleAgmRequest(DateTime.UtcNow, null), Ct);

        await _attendanceRepo.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<AgmAttendanceRecord>>(), Arg.Any<CancellationToken>());
        await _positionRepo.DidNotReceive().AddAsync(Arg.Any<CommitteePositionRecord>(), Arg.Any<CancellationToken>());
        await _termRepo.DidNotReceive().AddAsync(Arg.Any<CommitteeTerm>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScheduleAsync_WrapsWriteInTransaction()
    {
        var svc = CreateService();
        await svc.ScheduleAsync(new ScheduleAgmRequest(DateTime.UtcNow, null), Ct);

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScheduleAsync_LogsAuditCreate()
    {
        var svc = CreateService();
        var result = await svc.ScheduleAsync(new ScheduleAgmRequest(DateTime.UtcNow, null), Ct);

        await _audit.Received(1).LogAsync(nameof(AnnualGeneralMeeting), result.Id, Enums.AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScheduleAsync_ThrowsValidationException_AndPersistsNothing_WhenYearAlreadyHasAnAgm()
    {
        _agmRepo.ExistsForYearAsync(2026, Arg.Any<CancellationToken>()).Returns(true);

        var svc = CreateService();
        var request = new ScheduleAgmRequest(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), null);

        await Assert.ThrowsAsync<ValidationException>(() => svc.ScheduleAsync(request, Ct));
        await _unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
        await _agmRepo.DidNotReceive().AddAsync(Arg.Any<AnnualGeneralMeeting>(), Arg.Any<CancellationToken>());
    }

    // --- RecordAsync ---

    [Fact]
    public async Task RecordAsync_PersistsAttendanceAndPositions_Atomically()
    {
        var presidentTypeId = Guid.NewGuid();
        var presidentMemberId = Guid.NewGuid();
        var generalMemberId = Guid.NewGuid();
        var attendedMemberId = Guid.NewGuid();
        var notAttendedMemberId = Guid.NewGuid();
        var scheduled = SeedScheduledAgm();

        var request = MakeRequest(
            attended: [attendedMemberId],
            allActive: [attendedMemberId, notAttendedMemberId],
            officeHolders: new Dictionary<Guid, Guid> { [presidentTypeId] = presidentMemberId },
            generalCommittee: [generalMemberId]);

        _termRepo.GetOpenAsync(Arg.Any<CancellationToken>()).Returns((CommitteeTerm?)null);

        var svc = CreateService();
        var result = await svc.RecordAsync(scheduled.Id, request, Ct);

        Assert.Equal(scheduled.Date, result.Date);
        Assert.True(result.IsRecorded);
        Assert.Equal(5, result.GeneralCommitteeSeatCountTarget);

        await _attendanceRepo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<AgmAttendanceRecord>>(records =>
                records!.Count() == 2 &&
                records!.Single(r => r.MemberId == attendedMemberId).Attended &&
                !records!.Single(r => r.MemberId == notAttendedMemberId).Attended),
            Arg.Any<CancellationToken>());

        await _positionRepo.Received(1).AddAsync(
            Arg.Is<CommitteePositionRecord>(r => r!.MemberId == presidentMemberId && r.OfficeHolderTypeId == presidentTypeId),
            Arg.Any<CancellationToken>());
        await _positionRepo.Received(1).AddAsync(
            Arg.Is<CommitteePositionRecord>(r => r!.MemberId == generalMemberId && r.OfficeHolderTypeId == null),
            Arg.Any<CancellationToken>());

        await _audit.Received(1).LogAsync(nameof(AnnualGeneralMeeting), result.Id, Enums.AuditAction.Update,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_SetsIsRecordedTrue()
    {
        var scheduled = SeedScheduledAgm();
        _termRepo.GetOpenAsync(Arg.Any<CancellationToken>()).Returns((CommitteeTerm?)null);

        var svc = CreateService();
        var result = await svc.RecordAsync(scheduled.Id, MakeRequest(), Ct);

        Assert.True(result.IsRecorded);
        await _agmRepo.Received(1).UpdateAsync(Arg.Is<AnnualGeneralMeeting>(a => a!.IsRecorded), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_WrapsWriteInTransaction()
    {
        var scheduled = SeedScheduledAgm();
        _termRepo.GetOpenAsync(Arg.Any<CancellationToken>()).Returns((CommitteeTerm?)null);

        var svc = CreateService();
        await svc.RecordAsync(scheduled.Id, MakeRequest(), Ct);

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_ThrowsEntityNotFoundException_WhenAgmIdUnknown()
    {
        _agmRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((AnnualGeneralMeeting?)null);

        var svc = CreateService();
        await Assert.ThrowsAsync<EntityNotFoundException>(() => svc.RecordAsync(Guid.NewGuid(), MakeRequest(), Ct));
    }

    [Fact]
    public async Task RecordAsync_ThrowsValidationException_WhenAgmDateInFuture()
    {
        var scheduled = SeedScheduledAgm(date: DateTime.Today.AddDays(5));

        var svc = CreateService();
        await Assert.ThrowsAsync<ValidationException>(() => svc.RecordAsync(scheduled.Id, MakeRequest(), Ct));
        await _unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_ThrowsValidationException_WhenAgmAlreadyRecorded()
    {
        var agm = SeedScheduledAgm();
        agm.IsRecorded = true;

        var svc = CreateService();
        await Assert.ThrowsAsync<ValidationException>(() => svc.RecordAsync(agm.Id, MakeRequest(), Ct));
        await _unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_ThrowsValidationException_WhenMemberAppearsInOfficeHolderAndGeneralCommittee()
    {
        var scheduled = SeedScheduledAgm();
        var duplicateMemberId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var request = MakeRequest(
            officeHolders: new Dictionary<Guid, Guid> { [typeId] = duplicateMemberId },
            generalCommittee: [duplicateMemberId]);

        var svc = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => svc.RecordAsync(scheduled.Id, request, Ct));
        await _unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_ThrowsValidationException_WhenMemberHoldsTwoOfficeHolderAssignments()
    {
        var scheduled = SeedScheduledAgm();
        var duplicateMemberId = Guid.NewGuid();
        var request = MakeRequest(officeHolders: new Dictionary<Guid, Guid>
        {
            [Guid.NewGuid()] = duplicateMemberId,
            [Guid.NewGuid()] = duplicateMemberId
        });

        var svc = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => svc.RecordAsync(scheduled.Id, request, Ct));
    }

    [Fact]
    public async Task RecordAsync_RollsBackEntirely_OnMidTransactionFailure()
    {
        var scheduled = SeedScheduledAgm();
        _attendanceRepo.AddRangeAsync(Arg.Any<IEnumerable<AgmAttendanceRecord>>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new DataAccessException("simulated failure", nameof(AgmAttendanceRecord), "AddRangeAsync"));

        var svc = CreateService();
        var request = MakeRequest(allActive: [Guid.NewGuid()]);

        await Assert.ThrowsAsync<DataAccessException>(() => svc.RecordAsync(scheduled.Id, request, Ct));

        // Nothing downstream of the failure point should have been written.
        await _termRepo.DidNotReceive().AddAsync(Arg.Any<CommitteeTerm>(), Arg.Any<CancellationToken>());
        await _positionRepo.DidNotReceive().AddAsync(Arg.Any<CommitteePositionRecord>(), Arg.Any<CancellationToken>());
        await _audit.DidNotReceive().LogAsync(nameof(AnnualGeneralMeeting), Arg.Any<Guid>(), Enums.AuditAction.Update,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_ClosesPreviouslyOpenTerm_AndOpensNewOne()
    {
        var scheduled = SeedScheduledAgm(date: new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc));
        var openTerm = new CommitteeTerm
        {
            Id = Guid.NewGuid(), StartedByAgmId = Guid.NewGuid(), StartDate = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = null, LabelYear = 2025, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _termRepo.GetOpenAsync(Arg.Any<CancellationToken>()).Returns(openTerm);

        var svc = CreateService();
        await svc.RecordAsync(scheduled.Id, MakeRequest(), Ct);

        await _termRepo.Received(1).UpdateAsync(
            Arg.Is<CommitteeTerm>(t => t!.Id == openTerm.Id && t.EndDate == scheduled.Date),
            Arg.Any<CancellationToken>());
        await _termRepo.Received(1).AddAsync(
            Arg.Is<CommitteeTerm>(t => t!.Id != openTerm.Id && t.EndDate == null && t.StartDate == scheduled.Date),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_DoesNotCloseAnyTerm_WhenNoneIsOpen()
    {
        var scheduled = SeedScheduledAgm();
        _termRepo.GetOpenAsync(Arg.Any<CancellationToken>()).Returns((CommitteeTerm?)null);

        var svc = CreateService();
        await svc.RecordAsync(scheduled.Id, MakeRequest(), Ct);

        await _termRepo.DidNotReceive().UpdateAsync(Arg.Any<CommitteeTerm>(), Arg.Any<CancellationToken>());
        await _termRepo.Received(1).AddAsync(Arg.Any<CommitteeTerm>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_OctoberAgm_LabelsTermWithFollowingYear()
    {
        var scheduled = SeedScheduledAgm(date: new DateTime(2025, 10, 15, 0, 0, 0, DateTimeKind.Utc));
        _termRepo.GetOpenAsync(Arg.Any<CancellationToken>()).Returns((CommitteeTerm?)null);

        var svc = CreateService();
        await svc.RecordAsync(scheduled.Id, MakeRequest(), Ct);

        await _termRepo.Received(1).AddAsync(
            Arg.Is<CommitteeTerm>(t => t!.LabelYear == 2026), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_MarchAgm_LabelsTermWithItsOwnYear()
    {
        var scheduled = SeedScheduledAgm(date: new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc));
        _termRepo.GetOpenAsync(Arg.Any<CancellationToken>()).Returns((CommitteeTerm?)null);

        var svc = CreateService();
        await svc.RecordAsync(scheduled.Id, MakeRequest(), Ct);

        await _termRepo.Received(1).AddAsync(
            Arg.Is<CommitteeTerm>(t => t!.LabelYear == 2026), Arg.Any<CancellationToken>());
    }

    // --- GetByIdAsync ---

    [Fact]
    public async Task GetByIdAsync_DelegatesToRepository()
    {
        var id = Guid.NewGuid();
        var agm = new AnnualGeneralMeeting { Id = id, Date = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _agmRepo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(agm);

        var svc = CreateService();
        var result = await svc.GetByIdAsync(id, Ct);

        Assert.Same(agm, result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        _agmRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((AnnualGeneralMeeting?)null);

        var svc = CreateService();
        var result = await svc.GetByIdAsync(Guid.NewGuid(), Ct);

        Assert.Null(result);
    }

    // --- GetAllAsync ---

    [Fact]
    public async Task GetAllAsync_ReturnsMostRecentFirst_FromRepository()
    {
        var agms = new List<AnnualGeneralMeeting>
        {
            new() { Id = Guid.NewGuid(), Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Date = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };
        _agmRepo.GetPastOrderedAsync(Arg.Any<CancellationToken>()).Returns(agms);

        var svc = CreateService();
        var result = await svc.GetAllAsync(Ct);

        Assert.Same(agms, result);
    }

    // --- ArchiveAsync ---

    [Fact]
    public async Task ArchiveAsync_ArchivesAgm_AndCascadesToAttendanceRecords()
    {
        var agmId = Guid.NewGuid();
        var attendance = new List<AgmAttendanceRecord>
        {
            new() { Id = Guid.NewGuid(), AnnualGeneralMeetingId = agmId, MemberId = Guid.NewGuid(), Attended = true, CreatedAt = DateTime.UtcNow }
        };
        _attendanceRepo.GetByAgmAsync(agmId, Arg.Any<CancellationToken>()).Returns(attendance);

        var svc = CreateService();
        await svc.ArchiveAsync(agmId, "coordinator", Ct);

        await _agmRepo.Received(1).ArchiveAsync(agmId, "coordinator", Arg.Any<CancellationToken>());
        await _attendanceRepo.Received(1).UpdateAsync(
            Arg.Is<AgmAttendanceRecord>(a => a!.IsDeleted && a.DeletedBy == "coordinator"),
            Arg.Any<CancellationToken>());
        await _audit.Received(1).LogAsync(nameof(AnnualGeneralMeeting), agmId, Enums.AuditAction.Delete,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- RecordSpecialElectionAsync ---

    [Fact]
    public async Task RecordSpecialElectionAsync_ClosesOutgoing_AndCreatesIncoming()
    {
        var termId = Guid.NewGuid();
        var officeHolderTypeId = Guid.NewGuid();
        var outgoing = new CommitteePositionRecord
        {
            Id = Guid.NewGuid(), MemberId = Guid.NewGuid(), CommitteeTermId = termId, OfficeHolderTypeId = officeHolderTypeId,
            StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = null,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var term = new CommitteeTerm
        {
            Id = termId, StartedByAgmId = Guid.NewGuid(), StartDate = outgoing.StartDate!.Value, EndDate = null,
            LabelYear = 2026, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var incomingMemberId = Guid.NewGuid();
        var replacementDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        _positionRepo.GetByIdAsync(outgoing.Id, Arg.Any<CancellationToken>()).Returns(outgoing);
        _termRepo.GetByIdAsync(termId, Arg.Any<CancellationToken>()).Returns(term);
        _positionRepo.GetOpenByMemberInTermAsync(termId, incomingMemberId, Arg.Any<CancellationToken>())
            .Returns((CommitteePositionRecord?)null);

        var svc = CreateService();
        var request = new RecordSpecialElectionRequest(outgoing.Id, incomingMemberId, replacementDate);
        var incoming = await svc.RecordSpecialElectionAsync(request, Ct);

        Assert.Equal(replacementDate, outgoing.EndDate);
        Assert.Equal(incomingMemberId, incoming.MemberId);
        Assert.Equal(termId, incoming.CommitteeTermId);
        Assert.Equal(officeHolderTypeId, incoming.OfficeHolderTypeId);
        Assert.Equal(replacementDate, incoming.StartDate);
        Assert.Null(incoming.EndDate);
    }

    [Fact]
    public async Task RecordSpecialElectionAsync_ThrowsEntityNotFoundException_WhenOutgoingRecordDoesNotExist()
    {
        _positionRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((CommitteePositionRecord?)null);

        var svc = CreateService();
        var request = new RecordSpecialElectionRequest(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => svc.RecordSpecialElectionAsync(request, Ct));
    }

    [Fact]
    public async Task RecordSpecialElectionAsync_ThrowsDataIntegrityException_WhenTermAlreadyClosed()
    {
        var termId = Guid.NewGuid();
        var outgoing = new CommitteePositionRecord
        {
            Id = Guid.NewGuid(), MemberId = Guid.NewGuid(), CommitteeTermId = termId, OfficeHolderTypeId = Guid.NewGuid(),
            StartDate = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = null,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var closedTerm = new CommitteeTerm
        {
            Id = termId, StartedByAgmId = Guid.NewGuid(), StartDate = outgoing.StartDate!.Value,
            EndDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), LabelYear = 2025,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _positionRepo.GetByIdAsync(outgoing.Id, Arg.Any<CancellationToken>()).Returns(outgoing);
        _termRepo.GetByIdAsync(termId, Arg.Any<CancellationToken>()).Returns(closedTerm);

        var svc = CreateService();
        var request = new RecordSpecialElectionRequest(outgoing.Id, Guid.NewGuid(), DateTime.UtcNow);

        await Assert.ThrowsAsync<DataIntegrityException>(() => svc.RecordSpecialElectionAsync(request, Ct));
    }

    [Fact]
    public async Task RecordSpecialElectionAsync_ThrowsValidationException_WhenIncomingMemberAlreadyHoldsOpenSlotInTerm()
    {
        var termId = Guid.NewGuid();
        var incomingMemberId = Guid.NewGuid();
        var outgoing = new CommitteePositionRecord
        {
            Id = Guid.NewGuid(), MemberId = Guid.NewGuid(), CommitteeTermId = termId, OfficeHolderTypeId = Guid.NewGuid(),
            StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = null,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var term = new CommitteeTerm
        {
            Id = termId, StartedByAgmId = Guid.NewGuid(), StartDate = outgoing.StartDate!.Value, EndDate = null,
            LabelYear = 2026, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var existingOpenRecord = new CommitteePositionRecord
        {
            Id = Guid.NewGuid(), MemberId = incomingMemberId, CommitteeTermId = termId, OfficeHolderTypeId = null,
            StartDate = term.StartDate, EndDate = null, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _positionRepo.GetByIdAsync(outgoing.Id, Arg.Any<CancellationToken>()).Returns(outgoing);
        _termRepo.GetByIdAsync(termId, Arg.Any<CancellationToken>()).Returns(term);
        _positionRepo.GetOpenByMemberInTermAsync(termId, incomingMemberId, Arg.Any<CancellationToken>())
            .Returns(existingOpenRecord);

        var svc = CreateService();
        var request = new RecordSpecialElectionRequest(outgoing.Id, incomingMemberId, DateTime.UtcNow);

        await Assert.ThrowsAsync<ValidationException>(() => svc.RecordSpecialElectionAsync(request, Ct));
    }

    [Fact]
    public async Task RecordSpecialElectionAsync_WritesAuditEntries_ForOutgoingAndIncoming()
    {
        var termId = Guid.NewGuid();
        var outgoing = new CommitteePositionRecord
        {
            Id = Guid.NewGuid(), MemberId = Guid.NewGuid(), CommitteeTermId = termId, OfficeHolderTypeId = Guid.NewGuid(),
            StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = null,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var term = new CommitteeTerm
        {
            Id = termId, StartedByAgmId = Guid.NewGuid(), StartDate = outgoing.StartDate!.Value, EndDate = null,
            LabelYear = 2026, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _positionRepo.GetByIdAsync(outgoing.Id, Arg.Any<CancellationToken>()).Returns(outgoing);
        _termRepo.GetByIdAsync(termId, Arg.Any<CancellationToken>()).Returns(term);
        _positionRepo.GetOpenByMemberInTermAsync(termId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((CommitteePositionRecord?)null);

        var svc = CreateService();
        var request = new RecordSpecialElectionRequest(outgoing.Id, Guid.NewGuid(), DateTime.UtcNow);
        var incoming = await svc.RecordSpecialElectionAsync(request, Ct);

        await _audit.Received(1).LogAsync(nameof(CommitteePositionRecord), outgoing.Id, Enums.AuditAction.Update,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _audit.Received(1).LogAsync(nameof(CommitteePositionRecord), incoming.Id, Enums.AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
