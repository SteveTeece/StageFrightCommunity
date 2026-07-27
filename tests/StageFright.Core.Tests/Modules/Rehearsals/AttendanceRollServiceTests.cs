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
/// Unit tests for AttendanceRollService — rehearsal lookup, active-member filtering, sorting,
/// and Annual Fee Paid computation.
/// </summary>
public class AttendanceRollServiceTests : TestBase
{
    private readonly IRehearsalRepository _rehearsalRepo = Substitute.For<IRehearsalRepository>();
    private readonly IMemberService _memberService = Substitute.For<IMemberService>();
    private readonly IMemberBalanceService _memberBalanceService = Substitute.For<IMemberBalanceService>();
    private readonly IFeeRepository _feeRepo = Substitute.For<IFeeRepository>();

    private AttendanceRollService CreateService() =>
        new(_rehearsalRepo, _memberService, _memberBalanceService, _feeRepo);

    private void SetupSingleMember(Rehearsal rehearsal, Member member)
    {
        _rehearsalRepo.GetByIdAsync(rehearsal.Id, Arg.Any<CancellationToken>()).Returns(rehearsal);
        _memberService.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { member });
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
    public async Task GenerateAsync_Returns_OnlyActiveMembers_FromMemberService()
    {
        var svc = CreateService();
        var rehearsal = ARehearsal(Guid.NewGuid());
        var member = AMember("Alice", "Anderson");

        _rehearsalRepo.GetByIdAsync(rehearsal.Id, Arg.Any<CancellationToken>()).Returns(rehearsal);
        _memberService.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { member });

        var result = await svc.GenerateAsync(rehearsal.Id, Ct);

        Assert.Single(result.Members);
        Assert.Equal("Alice", result.Members[0].FirstName);
        Assert.Equal("Anderson", result.Members[0].LastName);
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
        _memberService.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { smithBob, jones, smithAlice });

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
        _memberService.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(new List<Member>());

        var result = await svc.GenerateAsync(rehearsal.Id, Ct);

        Assert.Empty(result.Members);
    }

    [Fact]
    public async Task GenerateAsync_Copies_RehearsalDateAndTime()
    {
        var svc = CreateService();
        var rehearsal = ARehearsal(Guid.NewGuid());

        _rehearsalRepo.GetByIdAsync(rehearsal.Id, Arg.Any<CancellationToken>()).Returns(rehearsal);
        _memberService.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(new List<Member>());

        var result = await svc.GenerateAsync(rehearsal.Id, Ct);

        Assert.Equal(rehearsal.Date, result.RehearsalDate);
        Assert.Equal(rehearsal.Time, result.RehearsalTime);
    }

    // --- Annual Fee Paid computation ---

    [Fact]
    public async Task GenerateAsync_AnnualFeePaid_True_WhenCurrentYearFeeFullySettled()
    {
        var svc = CreateService();
        var rehearsal = ARehearsal(Guid.NewGuid());
        var member = AMember("Alice", "Anderson");
        SetupSingleMember(rehearsal, member);

        _feeRepo.AnnualFeeExistsAsync(member.Id, DateTime.Today.Year, Arg.Any<CancellationToken>()).Returns(true);
        _memberBalanceService.GetOutstandingFeesAsync(member.Id, Arg.Any<CancellationToken>())
            .Returns(new List<OutstandingFee>()); // fully settled -> no outstanding entry

        var result = await svc.GenerateAsync(rehearsal.Id, Ct);

        Assert.True(result.Members[0].AnnualFeePaid);
    }

    [Fact]
    public async Task GenerateAsync_AnnualFeePaid_False_WhenCurrentYearFeeUnpaidOrPartial()
    {
        var svc = CreateService();
        var rehearsal = ARehearsal(Guid.NewGuid());
        var member = AMember("Bob", "Baker");
        SetupSingleMember(rehearsal, member);

        _feeRepo.AnnualFeeExistsAsync(member.Id, DateTime.Today.Year, Arg.Any<CancellationToken>()).Returns(true);
        _memberBalanceService.GetOutstandingFeesAsync(member.Id, Arg.Any<CancellationToken>())
            .Returns(new List<OutstandingFee>
            {
                new() { FeeId = Guid.NewGuid(), FeeType = FeeType.Annual, FeeDate = DateTime.Today, RemainingAmount = 25m }
            });

        var result = await svc.GenerateAsync(rehearsal.Id, Ct);

        Assert.False(result.Members[0].AnnualFeePaid);
    }

    [Fact]
    public async Task GenerateAsync_AnnualFeePaid_False_WhenNoCurrentYearFeeRecordExists()
    {
        var svc = CreateService();
        var rehearsal = ARehearsal(Guid.NewGuid());
        var member = AMember("Carol", "Clark");
        SetupSingleMember(rehearsal, member);

        _feeRepo.AnnualFeeExistsAsync(member.Id, DateTime.Today.Year, Arg.Any<CancellationToken>()).Returns(false);
        _memberBalanceService.GetOutstandingFeesAsync(member.Id, Arg.Any<CancellationToken>())
            .Returns(new List<OutstandingFee>());

        var result = await svc.GenerateAsync(rehearsal.Id, Ct);

        Assert.False(result.Members[0].AnnualFeePaid);
    }

    [Fact]
    public async Task GenerateAsync_AnnualFeePaid_True_WhenOverpaidOrCreditBalance()
    {
        var svc = CreateService();
        var rehearsal = ARehearsal(Guid.NewGuid());
        var member = AMember("Dave", "Davis");
        SetupSingleMember(rehearsal, member);

        // Overpaid: fee record exists, but GetOutstandingFeesAsync filters RemainingAmount <= 0 -> no entry
        _feeRepo.AnnualFeeExistsAsync(member.Id, DateTime.Today.Year, Arg.Any<CancellationToken>()).Returns(true);
        _memberBalanceService.GetOutstandingFeesAsync(member.Id, Arg.Any<CancellationToken>())
            .Returns(new List<OutstandingFee>());

        var result = await svc.GenerateAsync(rehearsal.Id, Ct);

        Assert.True(result.Members[0].AnnualFeePaid);
    }
}
