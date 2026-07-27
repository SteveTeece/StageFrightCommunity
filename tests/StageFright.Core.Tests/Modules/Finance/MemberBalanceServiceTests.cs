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
    public async Task GetAllMemberBalancesAsync_FiltersOutFeesAlreadyCoveredByPayments()
    {
        // Repository has no per-fee paid flag, so it returns full FIFO history (paid + unpaid).
        // The service must derive which prefix is paid off from the GL balance.
        var memberId = Guid.NewGuid();
        _memberRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Member> { MakeMember(memberId, "Mixed History") });
        _glRepo.GetMemberBalanceAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(4m); // 3 fees of $2 each = $6 total; $2 already paid off the oldest fee

        var oldestPaidFee = MakeFee(memberId, 2m);
        var unpaidFee1 = MakeFee(memberId, 2m);
        var unpaidFee2 = MakeFee(memberId, 2m);
        _feeRepo.GetUnpaidOrderedFifoAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(new List<Fee> { oldestPaidFee, unpaidFee1, unpaidFee2 });

        var result = await _sut.GetAllMemberBalancesAsync(Ct);

        var balance = Assert.Single(result, b => b.MemberId == memberId);
        Assert.Equal(4m, balance.Balance);
        Assert.Equal(2, balance.Fees.Count);
        Assert.DoesNotContain(oldestPaidFee, balance.Fees);
        Assert.Contains(unpaidFee1, balance.Fees);
        Assert.Contains(unpaidFee2, balance.Fees);
    }

    [Fact]
    public async Task GetBalanceAsync_DelegatesToGLRepository()
    {
        var balance = await _sut.GetBalanceAsync(MemberWithBalanceId, Ct);

        Assert.Equal(80m, balance);
    }

    // --- GetOutstandingFeesAsync ---

    [Fact]
    public async Task GetOutstandingFeesAsync_ReturnsTrueRemainingAmount_NotOriginalFeeAmount()
    {
        var memberId = Guid.NewGuid();
        var fee = MakeFee(memberId, 100m);
        _feeRepo.GetUnpaidOrderedFifoAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(new List<Fee> { fee });
        _glRepo.GetByFeeAsync(fee.Id, Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>
            {
                new() { AccountId = SystemAccounts.MemberReceivableId, DebitAmount = 100m, CreditAmount = 0m, FeeId = fee.Id },
                new() { AccountId = SystemAccounts.MemberReceivableId, DebitAmount = 0m, CreditAmount = 40m, FeeId = fee.Id },
            });

        var result = await _sut.GetOutstandingFeesAsync(memberId, Ct);

        var outstandingFee = Assert.Single(result);
        Assert.Equal(fee.Id, outstandingFee.FeeId);
        Assert.Equal(60m, outstandingFee.RemainingAmount);
    }

    [Fact]
    public async Task GetOutstandingFeesAsync_ExcludesFullySettledFees()
    {
        var memberId = Guid.NewGuid();
        var settledFee = MakeFee(memberId, 50m);
        var unsettledFee = MakeFee(memberId, 30m);
        _feeRepo.GetUnpaidOrderedFifoAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(new List<Fee> { settledFee, unsettledFee });
        _glRepo.GetByFeeAsync(settledFee.Id, Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>
            {
                new() { AccountId = SystemAccounts.MemberReceivableId, DebitAmount = 50m, CreditAmount = 0m, FeeId = settledFee.Id },
                new() { AccountId = SystemAccounts.MemberReceivableId, DebitAmount = 0m, CreditAmount = 50m, FeeId = settledFee.Id },
            });
        _glRepo.GetByFeeAsync(unsettledFee.Id, Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>
            {
                new() { AccountId = SystemAccounts.MemberReceivableId, DebitAmount = 30m, CreditAmount = 0m, FeeId = unsettledFee.Id },
            });

        var result = await _sut.GetOutstandingFeesAsync(memberId, Ct);

        var outstandingFee = Assert.Single(result);
        Assert.Equal(unsettledFee.Id, outstandingFee.FeeId);
    }

    [Fact]
    public async Task GetOutstandingFeesAsync_ReturnsEmptyList_ForMemberWithNoOutstandingFees()
    {
        var memberId = Guid.NewGuid();
        _feeRepo.GetUnpaidOrderedFifoAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(new List<Fee>());

        var result = await _sut.GetOutstandingFeesAsync(memberId, Ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetOutstandingFeesAsync_OrdersByFeeDateThenCreatedAtThenId()
    {
        var memberId = Guid.NewGuid();
        var newerFee = new Fee
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            MemberId = memberId, FeeType = FeeType.Annual, Amount = 20m,
            FeeDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var olderFee = new Fee
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            MemberId = memberId, FeeType = FeeType.Annual, Amount = 10m,
            FeeDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        // Supplied out of order — service must re-sort.
        _feeRepo.GetUnpaidOrderedFifoAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(new List<Fee> { newerFee, olderFee });
        _glRepo.GetByFeeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>());

        var result = await _sut.GetOutstandingFeesAsync(memberId, Ct);

        Assert.Equal(2, result.Count);
        Assert.Equal(olderFee.Id, result[0].FeeId);
        Assert.Equal(newerFee.Id, result[1].FeeId);
    }

    // --- Helpers ---

    private static Member MakeMember(Guid id, string name) => new()
    {
        Id = id,
        FirstName = name,
        LastName = "Test",
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
