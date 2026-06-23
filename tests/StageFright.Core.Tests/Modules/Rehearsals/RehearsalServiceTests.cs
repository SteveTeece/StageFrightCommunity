using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Rehearsals;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Rehearsals;

/// <summary>
/// Unit tests for RehearsalService — scheduling, rate formula, denominator exclusion, idempotency.
/// </summary>
public class RehearsalServiceTests : TestBase
{
    private readonly IRehearsalRepository _rehearsalRepo = Substitute.For<IRehearsalRepository>();
    private readonly IMemberRepository _memberRepo = Substitute.For<IMemberRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static readonly Guid RehearsalId = Guid.NewGuid();

    public RehearsalServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _rehearsalRepo.AddAsync(Arg.Any<Rehearsal>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Rehearsal>(0));
    }

    private RehearsalService CreateService() =>
        new(_rehearsalRepo, _memberRepo, _audit, _unitOfWork);

    // --- ScheduleAsync ---

    [Fact]
    public async Task ScheduleAsync_CreatesRehearsalWithCorrectFields()
    {
        var svc = CreateService();
        var request = new ScheduleRehearsalRequest
        {
            Date = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            Time = TimeSpan.FromHours(19),
            Notes = "Summer rehearsal"
        };

        var rehearsal = await svc.ScheduleAsync(request, Ct);

        Assert.Equal(request.Date, rehearsal.Date);
        Assert.Equal(request.Time, rehearsal.Time);
        Assert.Equal(request.Notes, rehearsal.Notes);
        Assert.Null(rehearsal.StoredAttendanceRate);
    }

    [Fact]
    public async Task ScheduleAsync_WritesAuditEntry()
    {
        var svc = CreateService();
        var request = new ScheduleRehearsalRequest
        {
            Date = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            Time = TimeSpan.FromHours(19)
        };

        await svc.ScheduleAsync(request, Ct);

        await _audit.Received(1).LogAsync(
            "Rehearsal", Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- FreezeAttendanceRateAsync ---

    [Fact]
    public async Task FreezeAttendanceRate_ComputesCorrectRate()
    {
        var rehearsal = ARehearsalWithNoRate();
        _rehearsalRepo.GetByIdAsync(RehearsalId, Arg.Any<CancellationToken>()).Returns(rehearsal);

        // 3 active members as of rehearsal date
        _memberRepo.GetActiveAsOfAsync(rehearsal.Date, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { ActiveMember(), ActiveMember(), ActiveMember() });

        var svc = CreateService();

        await svc.FreezeAttendanceRateAsync(RehearsalId, rehearsal.Date, presentCount: 2, Ct);

        // Rate = 2/3 * 100 ≈ 66.67
        await _rehearsalRepo.Received(1).UpdateAsync(
            Arg.Is<Rehearsal>(r => r.StoredAttendanceRate.HasValue && r.StoredAttendanceRate.Value > 66m && r.StoredAttendanceRate.Value < 67m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FreezeAttendanceRate_FullAttendance_Returns100()
    {
        var rehearsal = ARehearsalWithNoRate();
        _rehearsalRepo.GetByIdAsync(RehearsalId, Arg.Any<CancellationToken>()).Returns(rehearsal);

        _memberRepo.GetActiveAsOfAsync(rehearsal.Date, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { ActiveMember(), ActiveMember() });

        var svc = CreateService();

        await svc.FreezeAttendanceRateAsync(RehearsalId, rehearsal.Date, presentCount: 2, Ct);

        await _rehearsalRepo.Received(1).UpdateAsync(
            Arg.Is<Rehearsal>(r => r.StoredAttendanceRate == 100m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FreezeAttendanceRate_ExcludesArchivedMembers_FromDenominator()
    {
        var rehearsal = ARehearsalWithNoRate();
        _rehearsalRepo.GetByIdAsync(RehearsalId, Arg.Any<CancellationToken>()).Returns(rehearsal);

        // GetActiveAsOfAsync only returns non-archived active members (per contract)
        _memberRepo.GetActiveAsOfAsync(rehearsal.Date, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { ActiveMember() }); // 1 active only

        var svc = CreateService();

        await svc.FreezeAttendanceRateAsync(RehearsalId, rehearsal.Date, presentCount: 1, Ct);

        await _rehearsalRepo.Received(1).UpdateAsync(
            Arg.Is<Rehearsal>(r => r.StoredAttendanceRate == 100m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FreezeAttendanceRate_AlreadySet_IsIdempotent()
    {
        var rehearsal = ARehearsalWithNoRate();
        rehearsal.StoredAttendanceRate = 75m; // Already frozen
        _rehearsalRepo.GetByIdAsync(RehearsalId, Arg.Any<CancellationToken>()).Returns(rehearsal);

        var svc = CreateService();

        await svc.FreezeAttendanceRateAsync(RehearsalId, rehearsal.Date, presentCount: 5, Ct);

        // Rate must NOT be updated again
        await _rehearsalRepo.DidNotReceive().UpdateAsync(Arg.Any<Rehearsal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FreezeAttendanceRate_NoActiveMembers_SetsRateToZero()
    {
        var rehearsal = ARehearsalWithNoRate();
        _rehearsalRepo.GetByIdAsync(RehearsalId, Arg.Any<CancellationToken>()).Returns(rehearsal);
        _memberRepo.GetActiveAsOfAsync(rehearsal.Date, Arg.Any<CancellationToken>())
            .Returns(new List<Member>());

        var svc = CreateService();

        await svc.FreezeAttendanceRateAsync(RehearsalId, rehearsal.Date, presentCount: 0, Ct);

        await _rehearsalRepo.Received(1).UpdateAsync(
            Arg.Is<Rehearsal>(r => r.StoredAttendanceRate == 0m),
            Arg.Any<CancellationToken>());
    }

    // --- GetMostRecentPastAsync ---

    [Fact]
    public async Task GetMostRecentPastAsync_DelegatesToRepository()
    {
        var asOf = DateTime.UtcNow;
        var rehearsal = ARehearsalWithNoRate();
        _rehearsalRepo.GetMostRecentPastAsync(asOf, Arg.Any<CancellationToken>()).Returns(rehearsal);

        var svc = CreateService();
        var result = await svc.GetMostRecentPastAsync(asOf, Ct);

        Assert.Same(rehearsal, result);
    }

    // --- GetMostRecentPastWithoutAttendanceAsync ---

    [Fact]
    public async Task GetMostRecentPastWithoutAttendanceAsync_DelegatesToRepository()
    {
        var asOf = DateTime.UtcNow;
        var rehearsal = ARehearsalWithNoRate();
        _rehearsalRepo.GetMostRecentPastWithoutAttendanceAsync(asOf, Arg.Any<CancellationToken>()).Returns(rehearsal);

        var svc = CreateService();
        var result = await svc.GetMostRecentPastWithoutAttendanceAsync(asOf, Ct);

        Assert.Same(rehearsal, result);
    }

    [Fact]
    public async Task GetMostRecentPastWithoutAttendanceAsync_ReturnsNull_WhenAllRehearsalsHaveAttendance()
    {
        var asOf = DateTime.UtcNow;
        _rehearsalRepo.GetMostRecentPastWithoutAttendanceAsync(asOf, Arg.Any<CancellationToken>()).Returns((Rehearsal?)null);

        var svc = CreateService();
        var result = await svc.GetMostRecentPastWithoutAttendanceAsync(asOf, Ct);

        Assert.Null(result);
    }

    // --- GetMostRecentPastWithAttendanceAsync ---

    [Fact]
    public async Task GetMostRecentPastWithAttendanceAsync_DelegatesToRepository()
    {
        var asOf = DateTime.UtcNow;
        var rehearsal = ARehearsalWithNoRate();
        rehearsal.StoredAttendanceRate = 75m;
        _rehearsalRepo.GetMostRecentPastWithAttendanceAsync(asOf, Arg.Any<CancellationToken>()).Returns(rehearsal);

        var svc = CreateService();
        var result = await svc.GetMostRecentPastWithAttendanceAsync(asOf, Ct);

        Assert.Same(rehearsal, result);
    }

    [Fact]
    public async Task GetMostRecentPastWithAttendanceAsync_ReturnsNull_WhenNoRehearsalsHaveAttendance()
    {
        var asOf = DateTime.UtcNow;
        _rehearsalRepo.GetMostRecentPastWithAttendanceAsync(asOf, Arg.Any<CancellationToken>()).Returns((Rehearsal?)null);

        var svc = CreateService();
        var result = await svc.GetMostRecentPastWithAttendanceAsync(asOf, Ct);

        Assert.Null(result);
    }

    // --- Helpers ---

    private static Rehearsal ARehearsalWithNoRate() => new()
    {
        Id = RehearsalId,
        Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        Time = TimeSpan.FromHours(19),
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Member ActiveMember() => new()
    {
        Id = Guid.NewGuid(), Name = "Active", StreetAddress = "1 St",
        Status = MemberStatus.Active, ActivateDate = DateTime.UtcNow.Date,
        JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
