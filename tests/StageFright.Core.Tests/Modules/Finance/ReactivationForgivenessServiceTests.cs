using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for ReactivationForgivenessService:
/// prior-year default selection, GL write-off pairs, audit, fee record immutability.
/// </summary>
public class ReactivationForgivenessServiceTests : TestBase
{
    private readonly IFeeRepository _feeRepo = Substitute.For<IFeeRepository>();
    private readonly IGLRepository _glRepo = Substitute.For<IGLRepository>();
    private readonly IMemberRepository _memberRepo = Substitute.For<IMemberRepository>();
    private readonly ISettingsRepository _settingsRepo = Substitute.For<ISettingsRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid PriorFee2025Id = Guid.NewGuid();
    private static readonly Guid PriorFee2024Id = Guid.NewGuid();
    private static readonly Guid CurrentFeeId = Guid.NewGuid();
    private static readonly int CurrentYear = DateTime.UtcNow.Year;

    private readonly ReactivationForgivenessService _sut;

    public ReactivationForgivenessServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _feeRepo.GetByMemberAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(new List<Fee>
            {
                MakeFee(PriorFee2024Id, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), 50m),
                MakeFee(PriorFee2025Id, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), 50m),
                MakeFee(CurrentFeeId, new DateTime(CurrentYear, 1, 1, 0, 0, 0, DateTimeKind.Utc), 50m),
            });

        // Current tax rate used to split any Taxable fee's amount during a write-off.
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new Settings { Id = Guid.NewGuid(), OrganizationName = "Test", TaxRate = 10m });

        _sut = new ReactivationForgivenessService(_feeRepo, _glRepo, _memberRepo, _settingsRepo, _audit, _unitOfWork);
    }

    // --- GetForgivenessItemsAsync ---

    [Fact]
    public async Task GetForgivenessItems_ReturnAllFeesForMember()
    {
        var items = await _sut.GetForgivenessItemsAsync(MemberId, Ct);

        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task GetForgivenessItems_PriorYearFees_AreDefaultForgiven()
    {
        var items = await _sut.GetForgivenessItemsAsync(MemberId, Ct);

        var priorItems = items.Where(i => i.Year < CurrentYear).ToList();
        Assert.All(priorItems, i => Assert.True(i.IsDefaultForgiven));
    }

    [Fact]
    public async Task GetForgivenessItems_CurrentYearFee_IsNotDefaultForgiven()
    {
        var items = await _sut.GetForgivenessItemsAsync(MemberId, Ct);

        var current = items.Single(i => i.FeeId == CurrentFeeId);
        Assert.False(current.IsDefaultForgiven);
    }

    [Fact]
    public async Task GetForgivenessItems_IncludesCorrectYearAndAmount()
    {
        var items = await _sut.GetForgivenessItemsAsync(MemberId, Ct);

        var item2025 = items.Single(i => i.FeeId == PriorFee2025Id);
        Assert.Equal(2025, item2025.Year);
        Assert.Equal(50m, item2025.Amount);
    }

    [Fact]
    public async Task GetForgivenessItems_NoFees_ReturnsEmpty()
    {
        _feeRepo.GetByMemberAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(new List<Fee>());

        var items = await _sut.GetForgivenessItemsAsync(MemberId, Ct);

        Assert.Empty(items);
    }

    // --- ApplyForgivenessAsync ---

    [Fact]
    public async Task ApplyForgiveness_CreatesGLWriteOffPair_PerSelectedFee()
    {
        await _sut.ApplyForgivenessAsync(MemberId, new[] { PriorFee2025Id }, Ct);

        // Debit BadDebtExpense (9900) / Credit MemberReceivable (0101) per fee
        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Any(t => t.DebitAmount == 50m && t.GLAccount == "6999") &&
                lines!.Any(t => t.CreditAmount == 50m && t.GLAccount == "1200" && t.MemberId == MemberId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyForgiveness_MultipleSelectedFees_CreatesOnePairPerFee()
    {
        await _sut.ApplyForgivenessAsync(MemberId, new[] { PriorFee2024Id, PriorFee2025Id }, Ct);

        await _glRepo.Received(2).AddBalancedSetAsync(
            Arg.Any<IReadOnlyList<Transaction>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyForgiveness_RunsInsideUnitOfWork()
    {
        await _sut.ApplyForgivenessAsync(MemberId, new[] { PriorFee2025Id }, Ct);

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyForgiveness_WritesAuditEntry_PerFee()
    {
        await _sut.ApplyForgivenessAsync(MemberId, new[] { PriorFee2024Id, PriorFee2025Id }, Ct);

        await _audit.Received(2).LogAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), AuditAction.Forgiveness,
            Arg.Any<string?>(), Arg.Any<string?>(),
            "system", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyForgiveness_DoesNotCallFeeRepository_Update()
    {
        // Fee records must remain untouched — no update method on IFeeRepository
        await _sut.ApplyForgivenessAsync(MemberId, new[] { PriorFee2025Id }, Ct);

        // IFeeRepository has no UpdateAsync — only AddAsync, GetByIdAsync, etc.
        await _feeRepo.DidNotReceive().AddAsync(Arg.Any<Fee>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyForgiveness_EmptySelection_DoesNotCreateGLPairs()
    {
        await _sut.ApplyForgivenessAsync(MemberId, Array.Empty<Guid>(), Ct);

        await _glRepo.DidNotReceive().AddBalancedSetAsync(
            Arg.Any<IReadOnlyList<Transaction>>(), Arg.Any<CancellationToken>());
    }

    // --- GST decreasing adjustment ---

    [Fact]
    public async Task ApplyForgiveness_TaxableFee_Posts3Lines_DebitsBadDebtNetAndTaxCollected()
    {
        // A taxable $110-gross fee is forgiven: DR BadDebt net $100 / DR TaxCollected $10 / CR MemberReceivable gross $110.
        _feeRepo.GetByMemberAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(new List<Fee> { MakeFee(PriorFee2025Id, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), 110m, TaxCode.Taxable) });

        await _sut.ApplyForgivenessAsync(MemberId, new[] { PriorFee2025Id }, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 3
                && lines.Any(t => t.DebitAmount == 100m && t.GLAccount == "6999")
                && lines.Any(t => t.DebitAmount == 10m && t.AccountId == SystemAccounts.TaxCollectedId)
                && lines.Any(t => t.CreditAmount == 110m && t.GLAccount == "1200")
                && lines.All(t => t.TaxCode == TaxCode.Taxable)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(TaxCode.TaxExempt)]
    [InlineData(TaxCode.Excluded)]
    public async Task ApplyForgiveness_NonTaxableFee_Posts2Lines_NoTaxAdjustment(TaxCode? code)
    {
        _feeRepo.GetByMemberAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(new List<Fee> { MakeFee(PriorFee2025Id, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), 50m, code) });

        await _sut.ApplyForgivenessAsync(MemberId, new[] { PriorFee2025Id }, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 2
                && lines.Any(t => t.DebitAmount == 50m && t.GLAccount == "6999")
                && lines.Any(t => t.CreditAmount == 50m && t.GLAccount == "1200")
                && lines.All(t => t.TaxCode == code)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyForgiveness_GLPair_LinksFeeId()
    {
        await _sut.ApplyForgivenessAsync(MemberId, new[] { PriorFee2025Id }, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines => lines!.All(t => t.FeeId == PriorFee2025Id)),
            Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private static Fee MakeFee(Guid id, DateTime feeDate, decimal amount, TaxCode? taxCode = null) => new()
    {
        Id = id,
        MemberId = MemberId,
        FeeType = FeeType.Annual,
        Amount = amount,
        FeeDate = feeDate,
        DueDate = feeDate,
        PaidAtCreation = false,
        TaxCode = taxCode,
        CreatedAt = feeDate
    };
}
