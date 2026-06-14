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

        _sut = new PaymentService(_feeRepo, _paymentRepo, _glRepo, _audit, _unitOfWork);
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
            Arg.Is<Transaction>(t => t.DebitAmount == 30m && t.GLAccount == "0100"),
            Arg.Is<Transaction>(t => t.CreditAmount == 30m && t.GLAccount == "0101"),
            Arg.Any<CancellationToken>());

        // Pair 2: $80 for fee2
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 80m && t.GLAccount == "0100"),
            Arg.Is<Transaction>(t => t.CreditAmount == 80m && t.GLAccount == "0101"),
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
            Arg.Is<Transaction>(t => t.DebitAmount == 30m && t.GLAccount == "0100"),
            Arg.Is<Transaction>(t => t.CreditAmount == 30m && t.GLAccount == "0101"),
            Arg.Any<CancellationToken>());

        // Pair 2: $20 partial for fee2
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 20m && t.GLAccount == "0100"),
            Arg.Is<Transaction>(t => t.CreditAmount == 20m && t.GLAccount == "0101"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_Overpayment_CreatesExtraGLCreditPair()
    {
        // Payment of $130 — $20 overpayment after clearing $110 balance
        _glRepo.GetMemberBalanceAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(110m);

        await _sut.RecordAsync(MakeRequest(130m), Ct);

        // Overpayment pair: Debit MemberReceivable / Credit Cash for $20
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 20m && t.GLAccount == "0101"),
            Arg.Is<Transaction>(t => t.CreditAmount == 20m && t.GLAccount == "0100"),
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

        // Overpayment pair for the full $50 (entire amount is overpayment)
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 50m && t.GLAccount == "0101"),
            Arg.Is<Transaction>(t => t.CreditAmount == 50m && t.GLAccount == "0100"),
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
    public async Task RecordAsync_GLPairs_LinkPaymentId()
    {
        var payment = await _sut.RecordAsync(MakeRequest(30m), Ct);

        await _glRepo.Received().AddPairAsync(
            Arg.Is<Transaction>(t => t.PaymentId == payment.Id),
            Arg.Is<Transaction>(t => t.PaymentId == payment.Id),
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
