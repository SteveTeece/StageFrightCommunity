using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for MemberBalanceService: GL-derived balances and outstanding-only fee breakdown.
/// </summary>
public class MemberBalanceServiceTests : TestBase
{
    private readonly IMemberRepository _memberRepo = Substitute.For<IMemberRepository>();
    private readonly IFeeRepository _feeRepo = Substitute.For<IFeeRepository>();
    private readonly IGLRepository _glRepo = Substitute.For<IGLRepository>();

    private static readonly Guid MemberWithBalanceId = Guid.NewGuid();
    private static readonly Guid MemberFullyPaidId = Guid.NewGuid();

    private readonly MemberBalanceService _sut;

    public MemberBalanceServiceTests()
    {
        _memberRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Member>
            {
                MakeMember(MemberWithBalanceId, "Owes Money"),
                MakeMember(MemberFullyPaidId, "Paid Up"),
            });

        _glRepo.GetMemberBalanceAsync(MemberWithBalanceId, Arg.Any<CancellationToken>())
            .Returns(80m);
        _glRepo.GetMemberBalanceAsync(MemberFullyPaidId, Arg.Any<CancellationToken>())
            .Returns(0m);

        _feeRepo.GetUnpaidOrderedFifoAsync(MemberWithBalanceId, Arg.Any<CancellationToken>())
            .Returns(new List<Fee> { MakeFee(MemberWithBalanceId, 80m) });

        _sut = new MemberBalanceService(_memberRepo, _feeRepo, _glRepo);
    }

    [Fact]
    public async Task GetAllMemberBalancesAsync_ExcludesMembersWithZeroBalance()
    {
        var result = await _sut.GetAllMemberBalancesAsync(Ct);

        Assert.Single(result);
        Assert.Equal(MemberWithBalanceId, result[0].MemberId);
    }

    [Fact]
    public async Task GetAllMemberBalancesAsync_PopulatesOnlyUnpaidFees_NotAllFeeHistory()
    {
        var result = await _sut.GetAllMemberBalancesAsync(Ct);

        var balance = Assert.Single(result);
        Assert.Equal(80m, balance.Balance);
        Assert.Single(balance.Fees);

        // Must query the unpaid/outstanding-only repository method, never the full history.
        await _feeRepo.Received(1).GetUnpaidOrderedFifoAsync(MemberWithBalanceId, Arg.Any<CancellationToken>());
        await _feeRepo.DidNotReceive().GetByMemberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBalanceAsync_DelegatesToGLRepository()
    {
        var balance = await _sut.GetBalanceAsync(MemberWithBalanceId, Ct);

        Assert.Equal(80m, balance);
    }

    // --- Helpers ---

    private static Member MakeMember(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        StreetAddress = "1 Test St",
        JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private static Fee MakeFee(Guid memberId, decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        MemberId = memberId,
        FeeType = FeeType.Annual,
        Amount = amount,
        FeeDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        DueDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        PaidAtCreation = false,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };
}
