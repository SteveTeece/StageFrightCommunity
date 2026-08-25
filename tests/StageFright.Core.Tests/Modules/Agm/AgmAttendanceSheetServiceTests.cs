using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Agm;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Agm;

/// <summary>
/// Unit tests for AgmAttendanceSheetService — AGM lookup and mapping of either its fixed,
/// already-persisted attendance roster (recorded) or the currently-active member roster
/// (scheduled, FR-010).
/// </summary>
public class AgmAttendanceSheetServiceTests : TestBase
{
    private readonly IAgmRepository _agmRepo = Substitute.For<IAgmRepository>();
    private readonly IAgmAttendanceRepository _attendanceRepo = Substitute.For<IAgmAttendanceRepository>();
    private readonly IMemberRepository _memberRepo = Substitute.For<IMemberRepository>();

    private AgmAttendanceSheetService CreateService() => new(_agmRepo, _attendanceRepo, _memberRepo);

    private static AnnualGeneralMeeting AnAgm(Guid id, bool isRecorded = true) => new()
    {
        Id = id,
        Date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
        IsRecorded = isRecorded,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Member AMember(string firstName, string lastName, MemberStatus status = MemberStatus.Active) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = firstName,
        LastName = lastName,
        StreetAddress = "1 Test St",
        Status = status,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static AgmAttendanceRecord ARecord(Guid agmId, Member member, bool attended) => new()
    {
        Id = Guid.NewGuid(),
        AnnualGeneralMeetingId = agmId,
        MemberId = member.Id,
        Member = member,
        Attended = attended,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GenerateAsync_Throws_EntityNotFoundException_WhenAgmUnknown()
    {
        var svc = CreateService();
        var agmId = Guid.NewGuid();
        _agmRepo.GetByIdAsync(agmId, Arg.Any<CancellationToken>()).Returns((AnnualGeneralMeeting?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => svc.GenerateAsync(agmId, Ct));
    }

    // --- Recorded AGM (unchanged from spec 018) ---

    [Fact]
    public async Task GenerateAsync_Returns_RosterFromAttendanceRepository_WithAttendedCopiedUnchanged()
    {
        var svc = CreateService();
        var agm = AnAgm(Guid.NewGuid());
        var attendedMember = AMember("Alice", "Anderson");
        var absentMember = AMember("Bob", "Baker");

        _agmRepo.GetByIdAsync(agm.Id, Arg.Any<CancellationToken>()).Returns(agm);
        _attendanceRepo.GetByAgmAsync(agm.Id, Arg.Any<CancellationToken>())
            .Returns(new List<AgmAttendanceRecord>
            {
                ARecord(agm.Id, attendedMember, attended: true),
                ARecord(agm.Id, absentMember, attended: false)
            });

        var result = await svc.GenerateAsync(agm.Id, Ct);

        Assert.Equal(2, result.Members.Count);
        Assert.Contains(result.Members, m => m.LastName == "Anderson" && m.Attended);
        Assert.Contains(result.Members, m => m.LastName == "Baker" && !m.Attended);
    }

    [Fact]
    public async Task GenerateAsync_Returns_EmptyMembersList_WhenRecordedRosterEmpty()
    {
        var svc = CreateService();
        var agm = AnAgm(Guid.NewGuid());

        _agmRepo.GetByIdAsync(agm.Id, Arg.Any<CancellationToken>()).Returns(agm);
        _attendanceRepo.GetByAgmAsync(agm.Id, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());

        var result = await svc.GenerateAsync(agm.Id, Ct);

        Assert.Empty(result.Members);
    }

    [Fact]
    public async Task GenerateAsync_Copies_AgmDate()
    {
        var svc = CreateService();
        var agm = AnAgm(Guid.NewGuid());

        _agmRepo.GetByIdAsync(agm.Id, Arg.Any<CancellationToken>()).Returns(agm);
        _attendanceRepo.GetByAgmAsync(agm.Id, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());

        var result = await svc.GenerateAsync(agm.Id, Ct);

        Assert.Equal(agm.Date, result.AgmDate);
    }

    [Fact]
    public async Task GenerateAsync_Preserves_FirstNameAndLastName_FromMemberNavigation()
    {
        var svc = CreateService();
        var agm = AnAgm(Guid.NewGuid());
        var member = AMember("Carol", "Clark");

        _agmRepo.GetByIdAsync(agm.Id, Arg.Any<CancellationToken>()).Returns(agm);
        _attendanceRepo.GetByAgmAsync(agm.Id, Arg.Any<CancellationToken>())
            .Returns(new List<AgmAttendanceRecord> { ARecord(agm.Id, member, attended: true) });

        var result = await svc.GenerateAsync(agm.Id, Ct);

        Assert.Single(result.Members);
        Assert.Equal("Carol", result.Members[0].FirstName);
        Assert.Equal("Clark", result.Members[0].LastName);
    }

    [Fact]
    public async Task GenerateAsync_RecordedAgm_NeverCallsMemberRepository()
    {
        var svc = CreateService();
        var agm = AnAgm(Guid.NewGuid());

        _agmRepo.GetByIdAsync(agm.Id, Arg.Any<CancellationToken>()).Returns(agm);
        _attendanceRepo.GetByAgmAsync(agm.Id, Arg.Any<CancellationToken>()).Returns(new List<AgmAttendanceRecord>());

        await svc.GenerateAsync(agm.Id, Ct);

        await _memberRepo.DidNotReceive().GetByStatusAsync(Arg.Any<MemberStatus>(), Arg.Any<CancellationToken>());
    }

    // --- Scheduled AGM (NEW, FR-010) ---

    [Fact]
    public async Task GenerateAsync_ScheduledAgm_Returns_ActiveMembers_SortedBySurname_AllUnchecked()
    {
        var svc = CreateService();
        var agm = AnAgm(Guid.NewGuid(), isRecorded: false);
        var alice = AMember("Alice", "Zeta");
        var bob = AMember("Bob", "Alpha");

        _agmRepo.GetByIdAsync(agm.Id, Arg.Any<CancellationToken>()).Returns(agm);
        _memberRepo.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { alice, bob });

        var result = await svc.GenerateAsync(agm.Id, Ct);

        Assert.Equal(2, result.Members.Count);
        Assert.All(result.Members, m => Assert.False(m.Attended));
        Assert.Equal("Alpha", result.Members[0].LastName);
        Assert.Equal("Zeta", result.Members[1].LastName);
    }

    [Fact]
    public async Task GenerateAsync_ScheduledAgm_NeverCallsAttendanceRepository()
    {
        var svc = CreateService();
        var agm = AnAgm(Guid.NewGuid(), isRecorded: false);

        _agmRepo.GetByIdAsync(agm.Id, Arg.Any<CancellationToken>()).Returns(agm);
        _memberRepo.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>()).Returns(new List<Member>());

        await svc.GenerateAsync(agm.Id, Ct);

        await _attendanceRepo.DidNotReceive().GetByAgmAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_ScheduledAgm_ReturnsEmptyList_WhenNoActiveMembers()
    {
        var svc = CreateService();
        var agm = AnAgm(Guid.NewGuid(), isRecorded: false);

        _agmRepo.GetByIdAsync(agm.Id, Arg.Any<CancellationToken>()).Returns(agm);
        _memberRepo.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>()).Returns(new List<Member>());

        var result = await svc.GenerateAsync(agm.Id, Ct);

        Assert.Empty(result.Members);
    }
}
