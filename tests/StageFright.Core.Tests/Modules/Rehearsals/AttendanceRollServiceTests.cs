using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Rehearsals;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Rehearsals;

/// <summary>
/// Unit tests for AttendanceRollService — rehearsal lookup, point-in-time active-membership
/// filtering, sorting, and real Attended/RehearsalFeePaid computation.
/// </summary>
public class AttendanceRollServiceTests : TestBase
{
    private readonly IRehearsalRepository _rehearsalRepo = Substitute.For<IRehearsalRepository>();
    private readonly IMemberRepository _memberRepo = Substitute.For<IMemberRepository>();
    private readonly IAttendanceRepository _attendanceRepo = Substitute.For<IAttendanceRepository>();
    private readonly IMemberBalanceService _memberBalanceService = Substitute.For<IMemberBalanceService>();
    private readonly IFeeRepository _feeRepo = Substitute.For<IFeeRepository>();

    private AttendanceRollService CreateService() =>
        new(_rehearsalRepo, _memberRepo, _attendanceRepo, _memberBalanceService, _feeRepo);

    private void SetupSingleMember(Rehearsal rehearsal, Member member)
    {
        _rehearsalRepo.GetByIdAsync(rehearsal.Id, Arg.Any<CancellationToken>()).Returns(rehearsal);
        _memberRepo.GetActiveAsOfAsync(rehearsal.Date, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { member });
        _attendanceRepo.GetByRehearsalAsync(rehearsal.Id, Arg.Any<CancellationToken>())
            .Returns(new List<AttendanceRecord>());
        _feeRepo.GetByMemberAsync(member.Id, Arg.Any<CancellationToken>())
            .Returns(new List<Fee>());
    }

    private static Rehearsal ARehearsal(Guid id) => new()
    {
        Id = id,
        Date = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
        Time = TimeSpan.FromHours(19),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Member AMember(string firstName, string lastName) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = firstName,
        LastName = lastName,
        StreetAddress = "1 Test St",
        Status = MemberStatus.Active,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GenerateAsync_Throws_EntityNotFoundException_WhenRehearsalUnknown()
    {
        var svc = CreateService();
        var rehearsalId = Guid.NewGuid();
        _rehearsalRepo.GetByIdAsync(rehearsalId, Arg.Any<CancellationToken>()).Returns((Rehearsal?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => svc.GenerateAsync(rehearsalId, Ct));
    }

    [Fact]
    public async Task GenerateAsync_Returns_OnlyMembersActiveAsOfRehearsalDate()
    {
        var svc = CreateService();
        var rehearsal = ARehearsal(Guid.NewGuid());
        var member = AMember("Alice", "Anderson");
        SetupSingleMember(rehearsal, member);

        var result = await svc.GenerateAsync(rehearsal.Id, Ct);

        Assert.Single(result.Members);
        Assert.Equal("Alice", result.Members[0].FirstName);
        Assert.Equal("Anderson", result.Members[0].LastName);
        await _memberRepo.Received(1).GetActiveAsOfAsync(rehearsal.Date, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_Orders_BySurnameThenFirstName()
    {
        var svc = CreateService();
        var rehearsal = ARehearsal(Guid.NewGuid());
        var smithBob = AMember("Bob", "Smith");
        var smithAlice = AMember("Alice", "Smith");
        var jones = AMember("Zoe", "Jones");

        _rehearsalRepo.GetByIdAsync(rehearsal.Id, Arg.Any<CancellationToken>()).Returns(rehearsal);
        _memberRepo.GetActiveAsOfAsync(rehearsal.Date, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { smithBob, jones, smithAlice });
        _attendanceRepo.GetByRehearsalAsync(rehearsal.Id, Arg.Any<CancellationToken>())
            .Returns(new List<AttendanceRecord>());
        _feeRepo.GetByMemberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Fee>());

        var result = await svc.GenerateAsync(rehearsal.Id, Ct);

        Assert.Equal(3, result.Members.Count);
        Assert.Equal("Jones", result.Members[0].LastName);
        Assert.Equal("Smith", result.Members[1].LastName);
        Assert.Equal("Alice", result.Members[1].FirstName); // same-surname sub-sort by first name
        Assert.Equal("Smith", result.Members[2].LastName);
        Assert.Equal("Bob", result.Members[2].FirstName);
    }

    [Fact]
    public async Task GenerateAsync_Returns_EmptyMembersList_WhenNoActiveMembers()
    {
        var svc = CreateService();
        var rehearsal = ARehearsal(Guid.NewGuid());

        _rehearsalRepo.GetByIdAsync(rehearsal.Id, Arg.Any<CancellationToken>()).Returns(rehearsal);
        _memberRepo.GetActiveAsOfAsync(rehearsal.Date, Arg.Any<CancellationToken>())
            .Returns(new List<Member>());
        _attendanceRepo.GetByRehearsalAsync(rehearsal.Id, Arg.Any<CancellationToken>())
            .Returns(new List<AttendanceRecord>());

        var result = await svc.GenerateAsync(rehearsal.Id, Ct);

        Assert.Empty(result.Members);
    }

    [Fact]
    public async Task GenerateAsync_Copies_RehearsalDateAndTime()
    {
        var svc = CreateService();
        var rehearsal = ARehearsal(Guid.NewGuid());

        _rehearsalRepo.GetByIdAsync(rehearsal.Id, Arg.Any<CancellationToken>()).Returns(rehearsal);
        _memberRepo.GetActiveAsOfAsync(rehearsal.Date, Arg.Any<CancellationToken>())
            .Returns(new List<Member>());
        _attendanceRepo.GetByRehearsalAsync(rehearsal.Id, Arg.Any<CancellationToken>())
            .Returns(new List<AttendanceRecord>());

        var result = await svc.GenerateAsync(rehearsal.Id, Ct);

        Assert.Equal(rehearsal.Date, result.RehearsalDate);
        Assert.Equal(rehearsal.Time, result.RehearsalTime);
    }

    // --- Attended computation ---

    [Fact]
    public async Task GenerateAsync_Attended_False_WhenAttendanceNotYetRecorded()
    {
        var svc = CreateService();
        var rehearsal = ARehearsal(Guid.NewGuid());
        var member = AMember("Alice", "Anderson");
        SetupSingleMember(rehearsal, member);

        var result = await svc.GenerateAsync(rehearsal.Id, Ct);

        Assert.False(result.Members[0].Attended);
        Assert.False(result.Members[0].RehearsalFeePaid);
    }

    [Fact]
    public async Task GenerateAsync_Attended_True_WhenAttendanceRecordMarksPresent()
    {
        var svc = CreateService();
        var rehearsal = ARehearsal(Guid.NewGuid());
        var member = AMember("Bob", "Baker");
        SetupSingleMember(rehearsal, member);
        _attendanceRepo.GetByRehearsalAsync(rehearsal.Id, Arg.Any<CancellationToken>())
            .Returns(new List<AttendanceRecord>
            {
                new() { Id = Guid.NewGuid(), RehearsalId = rehearsal.Id, MemberId = member.Id, Attended = true, CreatedAt = DateTime.UtcNow }
            });

        var result = await svc.GenerateAsync(rehearsal.Id, Ct);

        Assert.True(result.Members[0].Attended);
    }

    [Fact]
    public async Task GenerateAsync_Attended_False_WhenRecordedAbsent()
    {
        var svc = CreateService();
        var rehearsal = ARehearsal(Guid.NewGuid());
        var member = AMember("Carol", "Clark");
        SetupSingleMember(rehearsal, member);
        _attendanceRepo.GetByRehearsalAsync(rehearsal.Id, Arg.Any<CancellationToken>())
            .Returns(new List<AttendanceRecord>
            {
                new() { Id = Guid.NewGuid(), RehearsalId = rehearsal.Id, MemberId = member.Id, Attended = false, CreatedAt = DateTime.UtcNow }
            });

        var result = await svc.GenerateAsync(rehearsal.Id, Ct);

        Assert.False(result.Members[0].Attended);
    }

    // --- RehearsalFeePaid computation ---

    [Fact]
    public async Task GenerateAsync_RehearsalFeePaid_True_WhenFeeFullySettled()
    {
        var svc = CreateService();
        var rehearsal = ARehearsal(Guid.NewGuid());
        var member = AMember("Dave", "Davis");
        SetupSingleMember(rehearsal, member);

        var fee = new Fee { Id = Guid.NewGuid(), MemberId = member.Id, FeeType = FeeType.Attendance, Amount = 5m, FeeDate = rehearsal.Date, DueDate = rehearsal.Date, RehearsalId = rehearsal.Id, CreatedAt = DateTime.UtcNow };
        _feeRepo.GetByMemberAsync(member.Id, Arg.Any<CancellationToken>()).Returns(new List<Fee> { fee });
        _memberBalanceService.GetOutstandingFeesAsync(member.Id, Arg.Any<CancellationToken>())
            .Returns(new List<OutstandingFee>()); // fully settled -> no outstanding entry

        var result = await svc.GenerateAsync(rehearsal.Id, Ct);

        Assert.True(result.Members[0].RehearsalFeePaid);
    }

    [Fact]
    public async Task GenerateAsync_RehearsalFeePaid_False_WhenAttendedButFeeMarkedUnpaid()
    {
        var svc = CreateService();
        var rehearsal = ARehearsal(Guid.NewGuid());
        var member = AMember("Erin", "Evans");
        SetupSingleMember(rehearsal, member);
        _attendanceRepo.GetByRehearsalAsync(rehearsal.Id, Arg.Any<CancellationToken>())
            .Returns(new List<AttendanceRecord>
            {
                new() { Id = Guid.NewGuid(), RehearsalId = rehearsal.Id, MemberId = member.Id, Attended = true, CreatedAt = DateTime.UtcNow }
            });

        var fee = new Fee { Id = Guid.NewGuid(), MemberId = member.Id, FeeType = FeeType.Attendance, Amount = 5m, FeeDate = rehearsal.Date, DueDate = rehearsal.Date, RehearsalId = rehearsal.Id, CreatedAt = DateTime.UtcNow };
        _feeRepo.GetByMemberAsync(member.Id, Arg.Any<CancellationToken>()).Returns(new List<Fee> { fee });
        _memberBalanceService.GetOutstandingFeesAsync(member.Id, Arg.Any<CancellationToken>())
            .Returns(new List<OutstandingFee>
            {
                new() { FeeId = fee.Id, FeeType = FeeType.Attendance, FeeDate = rehearsal.Date, DueDate = rehearsal.Date, RemainingAmount = 5m }
            });

        var result = await svc.GenerateAsync(rehearsal.Id, Ct);

        Assert.True(result.Members[0].Attended);
        Assert.False(result.Members[0].RehearsalFeePaid);
    }

    [Fact]
    public async Task GenerateAsync_RehearsalFeePaid_False_WhenNoFeeRecordedForRehearsal()
    {
        var svc = CreateService();
        var rehearsal = ARehearsal(Guid.NewGuid());
        var member = AMember("Frank", "Foster");
        SetupSingleMember(rehearsal, member);

        var result = await svc.GenerateAsync(rehearsal.Id, Ct);

        Assert.False(result.Members[0].RehearsalFeePaid);
    }
}
