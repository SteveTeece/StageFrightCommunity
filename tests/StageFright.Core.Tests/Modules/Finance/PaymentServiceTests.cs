using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for PaymentService: FIFO GL allocation, partial/overpayment, audit, Notes-only update.
/// </summary>
public class PaymentServiceTests : TestBase
{
    private readonly IFeeRepository _feeRepo = Substitute.For<IFeeRepository>();
    private readonly IPaymentRepository _paymentRepo = Substitute.For<IPaymentRepository>();
    private readonly IGLRepository _glRepo = Substitute.For<IGLRepository>();
    private readonly IMemberRepository _memberRepo = Substitute.For<IMemberRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid Fee1Id = Guid.NewGuid();
    private static readonly Guid Fee2Id = Guid.NewGuid();

    private readonly PaymentService _sut;

    public PaymentServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _paymentRepo.AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Payment>(0));

        // Default: member has $110 outstanding (fee1=$30, fee2=$80)
        _glRepo.GetMemberBalanceAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(110m);

        _feeRepo.GetUnpaidOrderedFifoAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(new List<Fee>
            {
                MakeFee(Fee1Id, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 30m),
                MakeFee(Fee2Id, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), 80m),
            });

        _sut = new PaymentService(_feeRepo, _paymentRepo, _glRepo, _memberRepo, _audit, _unitOfWork);
    }

    // --- RecordAsync: creates Payment ---

    [Fact]
    public async Task RecordAsync_CreatesPersistablePaymentRecord()
    {
        var request = MakeRequest(50m);

        var payment = await _sut.RecordAsync(request, Ct);

        Assert.Equal(MemberId, payment.MemberId);
        Assert.Equal(50m, payment.Amount);
        Assert.Equal(PaymentMethod.Cash, payment.PaymentMethod);
        await _paymentRepo.Received(1).AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_RunsInsideUnitOfWork()
    {
        await _sut.RecordAsync(MakeRequest(50m), Ct);

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_ThrowsValidation_WhenAmountIsZero()
    {
        await Assert.ThrowsAsync<Core.Exceptions.ValidationException>(
            () => _sut.RecordAsync(MakeRequest(0m), Ct));
    }

    [Fact]
    public async Task RecordAsync_ThrowsValidation_WhenAmountIsNegative()
    {
        await Assert.ThrowsAsync<Core.Exceptions.ValidationException>(
            () => _sut.RecordAsync(MakeRequest(-10m), Ct));
    }

    // --- FIFO allocation ---

    [Fact]
    public async Task RecordAsync_FullPayment_CreatesGLPairForEachFeeInFifoOrder()
    {
        // Payment of $110 covers both fees exactly
        await _sut.RecordAsync(MakeRequest(110m), Ct);

        // Pair 1: $30 for fee1 (oldest)
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 30m && t.GLAccount == "1100"),
            Arg.Is<Transaction>(t => t.CreditAmount == 30m && t.GLAccount == "1200"),
            Arg.Any<CancellationToken>());

        // Pair 2: $80 for fee2
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 80m && t.GLAccount == "1100"),
            Arg.Is<Transaction>(t => t.CreditAmount == 80m && t.GLAccount == "1200"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_PartialPayment_AllocatesFromOldestFeeFirst()
    {
        // Payment of $50 covers fee1 fully ($30) and part of fee2 ($20)
        _glRepo.GetMemberBalanceAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(110m);

        await _sut.RecordAsync(MakeRequest(50m), Ct);

        // Pair 1: $30 for fee1
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 30m && t.GLAccount == "1100"),
            Arg.Is<Transaction>(t => t.CreditAmount == 30m && t.GLAccount == "1200"),
            Arg.Any<CancellationToken>());

        // Pair 2: $20 partial for fee2
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 20m && t.GLAccount == "1100"),
            Arg.Is<Transaction>(t => t.CreditAmount == 20m && t.GLAccount == "1200"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_Overpayment_CreatesExtraGLCreditPair()
    {
        // Payment of $130 — $20 overpayment after clearing $110 balance
        _glRepo.GetMemberBalanceAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(110m);

        await _sut.RecordAsync(MakeRequest(130m), Ct);

        // Overpayment pair: Debit Cash / Credit MemberReceivable for $20
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 20m && t.GLAccount == "1100"),
            Arg.Is<Transaction>(t => t.CreditAmount == 20m && t.GLAccount == "1200"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_ZeroBalance_CreatesPaymentButNoAllocationPairs()
    {
        _glRepo.GetMemberBalanceAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(0m);

        await _sut.RecordAsync(MakeRequest(50m), Ct);

        // Payment record created
        await _paymentRepo.Received(1).AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());

        // Overpayment pair for the full $50 (entire amount is overpayment): Debit Cash / Credit MemberReceivable
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 50m && t.GLAccount == "1100"),
            Arg.Is<Transaction>(t => t.CreditAmount == 50m && t.GLAccount == "1200"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_CreatesAuditEntry_OnSuccess()
    {
        await _sut.RecordAsync(MakeRequest(50m), Ct);

        await _audit.Received(1).LogAsync(
            nameof(Payment), Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(),
            "system", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_SkipsFeesAlreadySettledByPriorPayments()
    {
        // Fee1 ($30) was fully settled by an earlier payment; only Fee2 ($80) is still owed.
        // GetUnpaidOrderedFifoAsync (per its real repository implementation) returns the
        // member's FULL fee history regardless of payment status — RecordAsync must consult
        // the GL per fee before allocating, not assume the list is already unpaid-only.
        _glRepo.GetMemberBalanceAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(80m);

        _glRepo.GetByFeeAsync(Fee1Id, Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>
            {
                new() { AccountId = SystemAccounts.MemberReceivableId, DebitAmount = 30m, CreditAmount = 0m, FeeId = Fee1Id },
                new() { AccountId = SystemAccounts.MemberReceivableId, DebitAmount = 0m, CreditAmount = 30m, FeeId = Fee1Id },
            });
        _glRepo.GetByFeeAsync(Fee2Id, Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>
            {
                new() { AccountId = SystemAccounts.MemberReceivableId, DebitAmount = 80m, CreditAmount = 0m, FeeId = Fee2Id },
            });

        // A new $80 payment should go entirely to Fee2 — Fee1 must not be touched again.
        await _sut.RecordAsync(MakeRequest(80m), Ct);

        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 80m && t.GLAccount == "1100" && t.FeeId == Fee2Id),
            Arg.Is<Transaction>(t => t.CreditAmount == 80m && t.GLAccount == "1200" && t.FeeId == Fee2Id),
            Arg.Any<CancellationToken>());

        await _glRepo.DidNotReceive().AddPairAsync(
            Arg.Is<Transaction>(t => t.FeeId == Fee1Id),
            Arg.Any<Transaction>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_GLPairs_LinkPaymentId()
    {
        var payment = await _sut.RecordAsync(MakeRequest(30m), Ct);

        await _glRepo.Received().AddPairAsync(
            Arg.Is<Transaction>(t => t.PaymentId == payment.Id),
            Arg.Is<Transaction>(t => t.PaymentId == payment.Id),
            Arg.Any<CancellationToken>());
    }

    // --- RecordAsync: SelectedFeeIds ---

    [Fact]
    public async Task RecordAsync_SelectedFeeIds_FullAllocation_WhenAmountEqualsCombinedRemainingTotal()
    {
        var request = MakeRequest(110m);
        request.SelectedFeeIds = [Fee1Id, Fee2Id];

        await _sut.RecordAsync(request, Ct);

        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 30m && t.FeeId == Fee1Id),
            Arg.Is<Transaction>(t => t.CreditAmount == 30m && t.FeeId == Fee1Id),
            Arg.Any<CancellationToken>());

        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 80m && t.FeeId == Fee2Id),
            Arg.Is<Transaction>(t => t.CreditAmount == 80m && t.FeeId == Fee2Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_SelectedFeeIds_PartialAllocation_OldestFirst_WhenAmountLessThanCombinedTotal()
    {
        var request = MakeRequest(50m);
        request.SelectedFeeIds = [Fee1Id, Fee2Id];

        await _sut.RecordAsync(request, Ct);

        // Fee1 (oldest) fully settled at $30
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 30m && t.FeeId == Fee1Id),
            Arg.Is<Transaction>(t => t.CreditAmount == 30m && t.FeeId == Fee1Id),
            Arg.Any<CancellationToken>());

        // Fee2 partially settled at $20 — remaining $60 untouched
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 20m && t.FeeId == Fee2Id),
            Arg.Is<Transaction>(t => t.CreditAmount == 20m && t.FeeId == Fee2Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_SelectedFeeIds_UncheckedFee_ReceivesZeroAllocation_EvenIfChronologicallyNext()
    {
        // Fee1 ($30, oldest) is deliberately NOT selected — only Fee2 ($80) is checked.
        var request = MakeRequest(80m);
        request.SelectedFeeIds = [Fee2Id];

        await _sut.RecordAsync(request, Ct);

        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 80m && t.FeeId == Fee2Id),
            Arg.Is<Transaction>(t => t.CreditAmount == 80m && t.FeeId == Fee2Id),
            Arg.Any<CancellationToken>());

        await _glRepo.DidNotReceive().AddPairAsync(
            Arg.Is<Transaction>(t => t.FeeId == Fee1Id),
            Arg.Any<Transaction>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_SelectedFeeIds_ThrowsValidation_WhenSelectionIsExplicitlyEmpty()
    {
        var request = MakeRequest(50m);
        request.SelectedFeeIds = [];

        await Assert.ThrowsAsync<Core.Exceptions.ValidationException>(
            () => _sut.RecordAsync(request, Ct));
    }

    [Fact]
    public async Task RecordAsync_SelectedFeeIds_ThrowsValidation_WhenAmountExceedsSelectedFeesRemainingTotal()
    {
        // Fee1's remaining total is $30 — request $50 against only Fee1.
        var request = MakeRequest(50m);
        request.SelectedFeeIds = [Fee1Id];

        await Assert.ThrowsAsync<Core.Exceptions.ValidationException>(
            () => _sut.RecordAsync(request, Ct));
    }

    [Fact]
    public async Task RecordAsync_SelectedFeeIds_UnrecognizedFeeId_ContributesNothing_AndDoesNotThrow()
    {
        var unrecognizedFeeId = Guid.NewGuid();
        var request = MakeRequest(30m);
        request.SelectedFeeIds = [Fee1Id, unrecognizedFeeId];

        var payment = await _sut.RecordAsync(request, Ct);

        Assert.Equal(30m, payment.Amount);
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 30m && t.FeeId == Fee1Id),
            Arg.Is<Transaction>(t => t.CreditAmount == 30m && t.FeeId == Fee1Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_SelectedFeeIdsNull_ReproducesUnfilteredFifoBehavior_ByteForByte()
    {
        // Regression: omitting SelectedFeeIds (null, the default) must behave exactly like
        // today's unfiltered FIFO allocation across the member's full unpaid fee history.
        var request = MakeRequest(110m);
        Assert.Null(request.SelectedFeeIds);

        await _sut.RecordAsync(request, Ct);

        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 30m && t.FeeId == Fee1Id),
            Arg.Is<Transaction>(t => t.CreditAmount == 30m && t.FeeId == Fee1Id),
            Arg.Any<CancellationToken>());
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 80m && t.FeeId == Fee2Id),
            Arg.Is<Transaction>(t => t.CreditAmount == 80m && t.FeeId == Fee2Id),
            Arg.Any<CancellationToken>());
    }

    // --- UpdateNotesAsync ---

    [Fact]
    public async Task UpdateNotesAsync_DelegatesToRepository()
    {
        var paymentId = Guid.NewGuid();
        await _sut.UpdateNotesAsync(paymentId, "new note", Ct);

        await _paymentRepo.Received(1).UpdateNotesAsync(paymentId, "new note", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateNotesAsync_AcceptsNullNotes()
    {
        var paymentId = Guid.NewGuid();
        await _sut.UpdateNotesAsync(paymentId, null, Ct);

        await _paymentRepo.Received(1).UpdateNotesAsync(paymentId, null, Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private static RecordPaymentRequest MakeRequest(decimal amount) => new()
    {
        MemberId = MemberId,
        Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        Amount = amount,
        PaymentMethod = PaymentMethod.Cash,
        PaymentType = PaymentType.Annual,
    };

    private static Fee MakeFee(Guid id, DateTime feeDate, decimal amount) => new()
    {
        Id = id,
        MemberId = MemberId,
        FeeType = FeeType.Annual,
        Amount = amount,
        FeeDate = feeDate,
        DueDate = feeDate,
        PaidAtCreation = false,
        CreatedAt = feeDate
    };
}
