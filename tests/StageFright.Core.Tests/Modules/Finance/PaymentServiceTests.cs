using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Localization.Resources;
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
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly IReceivedItemRepository _receivedItemRepo = Substitute.For<IReceivedItemRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid Fee1Id = Guid.NewGuid();
    private static readonly Guid Fee2Id = Guid.NewGuid();
    private static readonly Guid BankAccountId = Guid.NewGuid();
    private static readonly Guid NonBankAccountId = Guid.NewGuid();
    private const string BankAccountNumber = "1110";

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

        _accountRepo.GetByIdAsync(BankAccountId, Arg.Any<CancellationToken>())
            .Returns(new Account { Id = BankAccountId, AccountNumber = BankAccountNumber, Name = "Main Bank", Type = AccountType.Asset, IsBankAccount = true });
        _accountRepo.GetByIdAsync(NonBankAccountId, Arg.Any<CancellationToken>())
            .Returns(new Account { Id = NonBankAccountId, AccountNumber = "1300", Name = "Prepaid", Type = AccountType.Asset, IsBankAccount = false });

        _sut = new PaymentService(_feeRepo, _paymentRepo, _glRepo, _memberRepo, _accountRepo, _receivedItemRepo, _audit, _unitOfWork, RealLocalizer.Instance);
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
            Arg.Is<Transaction>(t => t!.DebitAmount == 30m && t.GLAccount == "1100"),
            Arg.Is<Transaction>(t => t!.CreditAmount == 30m && t.GLAccount == "1200"),
            Arg.Any<CancellationToken>());

        // Pair 2: $80 for fee2
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t!.DebitAmount == 80m && t.GLAccount == "1100"),
            Arg.Is<Transaction>(t => t!.CreditAmount == 80m && t.GLAccount == "1200"),
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
            Arg.Is<Transaction>(t => t!.DebitAmount == 30m && t.GLAccount == "1100"),
            Arg.Is<Transaction>(t => t!.CreditAmount == 30m && t.GLAccount == "1200"),
            Arg.Any<CancellationToken>());

        // Pair 2: $20 partial for fee2
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t!.DebitAmount == 20m && t.GLAccount == "1100"),
            Arg.Is<Transaction>(t => t!.CreditAmount == 20m && t.GLAccount == "1200"),
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
            Arg.Is<Transaction>(t => t!.DebitAmount == 20m && t.GLAccount == "1100"),
            Arg.Is<Transaction>(t => t!.CreditAmount == 20m && t.GLAccount == "1200"),
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
            Arg.Is<Transaction>(t => t!.DebitAmount == 50m && t.GLAccount == "1100"),
            Arg.Is<Transaction>(t => t!.CreditAmount == 50m && t.GLAccount == "1200"),
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
            Arg.Is<Transaction>(t => t!.DebitAmount == 80m && t.GLAccount == "1100" && t.FeeId == Fee2Id),
            Arg.Is<Transaction>(t => t!.CreditAmount == 80m && t.GLAccount == "1200" && t.FeeId == Fee2Id),
            Arg.Any<CancellationToken>());

        await _glRepo.DidNotReceive().AddPairAsync(
            Arg.Is<Transaction>(t => t!.FeeId == Fee1Id),
            Arg.Any<Transaction>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_GLPairs_LinkPaymentId()
    {
        var payment = await _sut.RecordAsync(MakeRequest(30m), Ct);

        await _glRepo.Received().AddPairAsync(
            Arg.Is<Transaction>(t => t!.PaymentId == payment.Id),
            Arg.Is<Transaction>(t => t!.PaymentId == payment.Id),
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
            Arg.Is<Transaction>(t => t!.DebitAmount == 30m && t.FeeId == Fee1Id),
            Arg.Is<Transaction>(t => t!.CreditAmount == 30m && t.FeeId == Fee1Id),
            Arg.Any<CancellationToken>());

        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t!.DebitAmount == 80m && t.FeeId == Fee2Id),
            Arg.Is<Transaction>(t => t!.CreditAmount == 80m && t.FeeId == Fee2Id),
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
            Arg.Is<Transaction>(t => t!.DebitAmount == 30m && t.FeeId == Fee1Id),
            Arg.Is<Transaction>(t => t!.CreditAmount == 30m && t.FeeId == Fee1Id),
            Arg.Any<CancellationToken>());

        // Fee2 partially settled at $20 — remaining $60 untouched
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t!.DebitAmount == 20m && t.FeeId == Fee2Id),
            Arg.Is<Transaction>(t => t!.CreditAmount == 20m && t.FeeId == Fee2Id),
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
            Arg.Is<Transaction>(t => t!.DebitAmount == 80m && t.FeeId == Fee2Id),
            Arg.Is<Transaction>(t => t!.CreditAmount == 80m && t.FeeId == Fee2Id),
            Arg.Any<CancellationToken>());

        await _glRepo.DidNotReceive().AddPairAsync(
            Arg.Is<Transaction>(t => t!.FeeId == Fee1Id),
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
            Arg.Is<Transaction>(t => t!.DebitAmount == 30m && t.FeeId == Fee1Id),
            Arg.Is<Transaction>(t => t!.CreditAmount == 30m && t.FeeId == Fee1Id),
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
            Arg.Is<Transaction>(t => t!.DebitAmount == 30m && t.FeeId == Fee1Id),
            Arg.Is<Transaction>(t => t!.CreditAmount == 30m && t.FeeId == Fee1Id),
            Arg.Any<CancellationToken>());
        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t!.DebitAmount == 80m && t.FeeId == Fee2Id),
            Arg.Is<Transaction>(t => t!.CreditAmount == 80m && t.FeeId == Fee2Id),
            Arg.Any<CancellationToken>());
    }

    // --- Routing by payment method (spec 033) ---

    [Fact]
    public async Task Should_DebitPettyCashAndAddNoItem_When_MethodIsCash()
    {
        await _sut.RecordAsync(MakeRequest(110m), Ct);

        await _glRepo.Received(2).AddPairAsync(
            Arg.Is<Transaction>(t => t!.AccountId == SystemAccounts.CashId && t.GLAccount == SystemAccounts.CashNumber),
            Arg.Any<Transaction>(),
            Arg.Any<CancellationToken>());
        await _receivedItemRepo.DidNotReceive().AddAsync(Arg.Any<ReceivedItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_IgnoreBankAccountAndReference_When_MethodIsCash()
    {
        var request = MakeRequest(30m);
        request.BankAccountId = BankAccountId;
        request.Reference = "123456";

        await _sut.RecordAsync(request, Ct);

        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t!.AccountId == SystemAccounts.CashId),
            Arg.Any<Transaction>(),
            Arg.Any<CancellationToken>());
        await _accountRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _receivedItemRepo.DidNotReceive().AddAsync(Arg.Any<ReceivedItem>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(PaymentMethod.Check)]
    [InlineData(PaymentMethod.Other)]
    public async Task Should_DebitUndepositedFundsAndAddOneItem_When_MethodIsChequeOrOther(PaymentMethod method)
    {
        var request = MakeRequest(30m, method);
        request.Reference = "  000123  ";

        var payment = await _sut.RecordAsync(request, Ct);

        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t!.AccountId == SystemAccounts.UndepositedFundsId
                && t.GLAccount == SystemAccounts.UndepositedFundsNumber && t.DebitAmount == 30m),
            Arg.Is<Transaction>(t => t!.AccountId == SystemAccounts.MemberReceivableId && t.CreditAmount == 30m),
            Arg.Any<CancellationToken>());
        await _receivedItemRepo.Received(1).AddAsync(
            Arg.Is<ReceivedItem>(i => i!.PaymentId == payment.Id && i.JournalEntryId == null
                && i.Amount == 30m && i.Method == method && i.Reference == "000123"
                && i.Date == request.Date),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_AddOneItemForWholePayment_When_ChequePaysSeveralFeesAndOverpays()
    {
        // $130 cheque: $30 to fee1, $80 to fee2, $20 overpayment — three GL pairs, one item.
        var payment = await _sut.RecordAsync(MakeRequest(130m, PaymentMethod.Check), Ct);

        await _glRepo.Received(3).AddPairAsync(
            Arg.Is<Transaction>(t => t!.AccountId == SystemAccounts.UndepositedFundsId),
            Arg.Any<Transaction>(),
            Arg.Any<CancellationToken>());
        await _receivedItemRepo.Received(1).AddAsync(Arg.Any<ReceivedItem>(), Arg.Any<CancellationToken>());
        await _receivedItemRepo.Received(1).AddAsync(
            Arg.Is<ReceivedItem>(i => i!.Amount == 130m && i.PaymentId == payment.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_StoreNullReference_When_ChequeReferenceIsBlank()
    {
        var request = MakeRequest(30m, PaymentMethod.Check);
        request.Reference = "   ";

        await _sut.RecordAsync(request, Ct);

        await _receivedItemRepo.Received(1).AddAsync(
            Arg.Is<ReceivedItem>(i => i!.Reference == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_RefuseAndPostNothing_When_ChequeReferenceIsOverFiftyCharacters()
    {
        var request = MakeRequest(30m, PaymentMethod.Check);
        request.Reference = new string('9', 51);

        var ex = await Assert.ThrowsAsync<Core.Exceptions.ValidationException>(() => _sut.RecordAsync(request, Ct));

        Assert.Equal("The cheque reference must be 50 characters or fewer.", ex.Message);
        await _paymentRepo.DidNotReceive().AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
        await _receivedItemRepo.DidNotReceive().AddAsync(Arg.Any<ReceivedItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_KeepWholeReference_When_ChequeReferenceIsExactlyFiftyCharacters()
    {
        var request = MakeRequest(30m, PaymentMethod.Check);
        request.Reference = "  " + new string('9', 50) + "  ";

        await _sut.RecordAsync(request, Ct);

        await _receivedItemRepo.Received(1).AddAsync(
            Arg.Is<ReceivedItem>(i => i!.Reference == new string('9', 50)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_IgnoreLongReference_When_MethodIsCash()
    {
        var request = MakeRequest(30m, PaymentMethod.Cash);
        request.Reference = new string('9', 60);

        await _sut.RecordAsync(request, Ct);

        await _receivedItemRepo.DidNotReceive().AddAsync(Arg.Any<ReceivedItem>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(PaymentMethod.Card)]
    [InlineData(PaymentMethod.ElectronicTransfer)]
    public async Task Should_DebitChosenBankAccountAndAddNoItem_When_MethodIsCardOrTransfer(PaymentMethod method)
    {
        var request = MakeRequest(130m, method);
        request.BankAccountId = BankAccountId;
        request.Reference = "ignored";

        await _sut.RecordAsync(request, Ct);

        // Two fee allocations plus the overpayment, all to the chosen bank.
        await _glRepo.Received(3).AddPairAsync(
            Arg.Is<Transaction>(t => t!.AccountId == BankAccountId && t.GLAccount == BankAccountNumber),
            Arg.Is<Transaction>(t => t!.AccountId == SystemAccounts.MemberReceivableId),
            Arg.Any<CancellationToken>());
        await _receivedItemRepo.DidNotReceive().AddAsync(Arg.Any<ReceivedItem>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(PaymentMethod.Card)]
    [InlineData(PaymentMethod.ElectronicTransfer)]
    public async Task Should_ThrowValidationAndPostNothing_When_CardOrTransferHasNoBankAccount(PaymentMethod method)
    {
        var request = MakeRequest(30m, method);

        await AssertRefusedWithInvalidBankAsync(request);
    }

    [Fact]
    public async Task Should_ThrowValidationAndPostNothing_When_BankAccountIsPettyCash()
    {
        var request = MakeRequest(30m, PaymentMethod.Card);
        request.BankAccountId = SystemAccounts.CashId;

        await AssertRefusedWithInvalidBankAsync(request);
    }

    [Fact]
    public async Task Should_ThrowValidationAndPostNothing_When_BankAccountDoesNotExist()
    {
        var request = MakeRequest(30m, PaymentMethod.ElectronicTransfer);
        request.BankAccountId = Guid.NewGuid();

        await AssertRefusedWithInvalidBankAsync(request);
    }

    [Fact]
    public async Task Should_ThrowValidationAndPostNothing_When_AccountIsNotABankAccount()
    {
        var request = MakeRequest(30m, PaymentMethod.Card);
        request.BankAccountId = NonBankAccountId;

        await AssertRefusedWithInvalidBankAsync(request);
    }

    [Fact]
    public async Task Should_AddNoItem_When_ChequePaymentIsRefusedForEmptyFeeSelection()
    {
        var request = MakeRequest(30m, PaymentMethod.Check);
        request.SelectedFeeIds = [];

        await Assert.ThrowsAsync<Core.Exceptions.ValidationException>(() => _sut.RecordAsync(request, Ct));

        await _receivedItemRepo.DidNotReceive().AddAsync(Arg.Any<ReceivedItem>(), Arg.Any<CancellationToken>());
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

    private static RecordPaymentRequest MakeRequest(decimal amount, PaymentMethod method = PaymentMethod.Cash) => new()
    {
        MemberId = MemberId,
        Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        Amount = amount,
        PaymentMethod = method,
        PaymentType = PaymentType.Annual,
    };

    private async Task AssertRefusedWithInvalidBankAsync(RecordPaymentRequest request)
    {
        var ex = await Assert.ThrowsAsync<Core.Exceptions.ValidationException>(() => _sut.RecordAsync(request, Ct));

        Assert.Equal(RealLocalizer.Instance.Get<ValidationResource>("Validation_Deposit_BankAccountInvalid"), ex.Message);
        await _unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
        await _paymentRepo.DidNotReceive().AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
    }

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
