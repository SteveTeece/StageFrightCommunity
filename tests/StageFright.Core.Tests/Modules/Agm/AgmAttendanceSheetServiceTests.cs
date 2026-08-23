using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Agm;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Agm;

/// <summary>
/// Unit tests for AgmAttendanceSheetService — AGM lookup and mapping of its fixed, already-
/// persisted attendance roster.
/// </summary>
public class AgmAttendanceSheetServiceTests : TestBase
{
    private readonly IAgmRepository _agmRepo = Substitute.For<IAgmRepository>();
    private readonly IAgmAttendanceRepository _attendanceRepo = Substitute.For<IAgmAttendanceRepository>();

    private AgmAttendanceSheetService CreateService() => new(_agmRepo, _attendanceRepo);

    private static AnnualGeneralMeeting AnAgm(Guid id) => new()
    {
        Id = id,
        Date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Member AMember(string firstName, string lastName) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = firstName,
        LastName = lastName,
        StreetAddress = "1 Test St",
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
    public async Task GenerateAsync_Returns_EmptyMembersList_WhenRosterEmpty()
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
}
