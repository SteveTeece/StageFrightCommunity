using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Records member payments with FIFO GL allocation.
/// Per fee in FIFO order: Debit money side / Credit MemberReceivable (1200).
/// Overpayment (payment > balance): Debit money side / Credit MemberReceivable (1200) — creates negative (credit) balance.
/// The money side follows <see cref="ReceiptRouting"/> (spec 033): Petty Cash for cash,
/// Undeposited Funds for cheque and other (plus one <see cref="ReceivedItem"/> for the whole
/// payment), and the chosen bank account for card and electronic transfer.
/// </summary>
public class PaymentService : IPaymentService
{
    private const int ReferenceMaxLength = 50;

    private readonly IFeeRepository _feeRepo;
    private readonly IPaymentRepository _paymentRepo;
    private readonly IGLRepository _glRepo;
    private readonly IMemberRepository _memberRepo;
    private readonly IAccountRepository _accountRepo;
    private readonly IReceivedItemRepository _receivedItemRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizer _localizer;

    public PaymentService(
        IFeeRepository feeRepo,
        IPaymentRepository paymentRepo,
        IGLRepository glRepo,
        IMemberRepository memberRepo,
        IAccountRepository accountRepo,
        IReceivedItemRepository receivedItemRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork,
        ILocalizer localizer)
    {
        _feeRepo = feeRepo;
        _paymentRepo = paymentRepo;
        _glRepo = glRepo;
        _memberRepo = memberRepo;
        _accountRepo = accountRepo;
        _receivedItemRepo = receivedItemRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    public async Task<Payment> RecordAsync(RecordPaymentRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0m)
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_Payment_AmountPositive"),
                nameof(Payment), nameof(RecordAsync));

        var moneyAccountNumber = await ResolveMoneyAccountNumberAsync(request, ct);
        var moneyAccountId = ReceiptRouting.Destination(request.PaymentMethod, request.BankAccountId);

        Payment savedPayment = null!;

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;

            // 1. Persist the Payment record
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                MemberId = request.MemberId,
                Date = request.Date,
                Amount = request.Amount,
                PaymentMethod = request.PaymentMethod,
                PaymentType = request.PaymentType,
                Notes = request.Notes,
                CreatedAt = now,
                UpdatedAt = now
            };
            savedPayment = await _paymentRepo.AddAsync(payment, innerCt);

            var member = await _memberRepo.GetByIdAsync(request.MemberId, innerCt);
            var memberName = member?.FullName ?? "Unknown Member";

            // 2. FIFO allocation: get outstanding balance and fees in order
            var outstandingBalance = await _glRepo.GetMemberBalanceAsync(request.MemberId, innerCt);
            var fees = await _feeRepo.GetUnpaidOrderedFifoAsync(request.MemberId, innerCt);

            if (request.SelectedFeeIds is not null)
            {
                if (request.SelectedFeeIds.Count == 0)
                    throw new ValidationException(
                        _localizer.Get<ValidationResource>("Validation_Payment_SelectAtLeastOneFee"),
                        nameof(Payment), nameof(RecordAsync));

                var selectedSet = request.SelectedFeeIds.ToHashSet();
                var selectedFees = fees.Where(f => selectedSet.Contains(f.Id)).ToList();

                decimal selectedRemainingTotal = 0m;
                foreach (var fee in selectedFees)
                {
                    var feeTransactions = await _glRepo.GetByFeeAsync(fee.Id, innerCt);
                    var alreadySettled = feeTransactions
                        .Where(t => t.AccountId == SystemAccounts.MemberReceivableId)
                        .Sum(t => t.CreditAmount);
                    var remainingOwedOnFee = fee.Amount - alreadySettled;
                    if (remainingOwedOnFee > 0m)
                        selectedRemainingTotal += remainingOwedOnFee;
                }

                if (request.Amount > selectedRemainingTotal)
                    throw new ValidationException(
                        _localizer.Get<ValidationResource>("Validation_Payment_AmountExceedsSelectedTotal"),
                        nameof(Payment), nameof(RecordAsync));

                fees = selectedFees;
            }

            decimal remainingPayment = request.Amount;

            if (outstandingBalance > 0m && fees.Count > 0)
            {
                // Allocate against fees in FIFO order (oldest first). GetUnpaidOrderedFifoAsync
                // returns the member's full fee history, not just what's unpaid — fees carry no
                // paid flag (GL is authoritative), so each fee's already-settled amount (from prior
                // payments or forgiveness write-offs) must be read back from the GL before allocating.
                foreach (var fee in fees)
                {
                    if (remainingPayment <= 0m)
                        break;

                    var feeTransactions = await _glRepo.GetByFeeAsync(fee.Id, innerCt);
                    var alreadySettled = feeTransactions
                        .Where(t => t.AccountId == SystemAccounts.MemberReceivableId)
                        .Sum(t => t.CreditAmount);
                    var remainingOwedOnFee = fee.Amount - alreadySettled;

                    if (remainingOwedOnFee <= 0m)
                        continue;

                    // Amount to apply to this fee: limited by payment remaining and what's still owed
                    decimal allocation = Math.Min(remainingPayment, remainingOwedOnFee);

                    await _glRepo.AddPairAsync(
                        new Transaction
                        {
                            Id = Guid.NewGuid(),
                            Date = request.Date,
                            AccountId = moneyAccountId,
                            DebitAmount = allocation,
                            CreditAmount = 0m,
                            GLAccount = moneyAccountNumber,
                            MemberId = request.MemberId,
                            PaymentId = savedPayment.Id,
                            FeeId = fee.Id,
                            Description = $"Payment from {memberName} — {fee.FeeType} fee allocation",
                            CreatedAt = now
                        },
                        new Transaction
                        {
                            Id = Guid.NewGuid(),
                            Date = request.Date,
                            AccountId = SystemAccounts.MemberReceivableId,
                            CreditAmount = allocation,
                            DebitAmount = 0m,
                            GLAccount = SystemAccounts.MemberReceivableNumber,
                            MemberId = request.MemberId,
                            PaymentId = savedPayment.Id,
                            FeeId = fee.Id,
                            Description = $"Payment from {memberName} — receivable cleared",
                            CreatedAt = now
                        },
                        innerCt);

                    remainingPayment -= allocation;
                }
            }

            // 3. Overpayment: credit the member's receivable account (negative balance = credit owed)
            if (remainingPayment > 0m)
            {
                await _glRepo.AddPairAsync(
                    new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = request.Date,
                        AccountId = moneyAccountId,
                        DebitAmount = remainingPayment,
                        CreditAmount = 0m,
                        GLAccount = moneyAccountNumber,
                        MemberId = request.MemberId,
                        PaymentId = savedPayment.Id,
                        Description = $"Overpayment received from {memberName}",
                        CreatedAt = now
                    },
                    new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = request.Date,
                        AccountId = SystemAccounts.MemberReceivableId,
                        DebitAmount = 0m,
                        CreditAmount = remainingPayment,
                        GLAccount = SystemAccounts.MemberReceivableNumber,
                        MemberId = request.MemberId,
                        PaymentId = savedPayment.Id,
                        Description = $"Overpayment credit to {memberName}'s account",
                        CreatedAt = now
                    },
                    innerCt);
            }

            // 4. Cheque or other: one item awaiting deposit for the whole payment, however many fees it paid
            if (ReceiptRouting.CreatesReceivedItem(request.PaymentMethod))
            {
                await _receivedItemRepo.AddAsync(new ReceivedItem
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    Amount = savedPayment.Amount,
                    Method = request.PaymentMethod,
                    Reference = NormaliseReference(request.Reference),
                    PaymentId = savedPayment.Id,
                    CreatedAt = now
                }, innerCt);
            }

            // 5. Audit
            await _audit.LogAsync(
                nameof(Payment), savedPayment.Id, AuditAction.Create,
                oldValue: null,
                newValue: $"Payment {request.Amount:C} by {request.PaymentMethod} from member {request.MemberId} on {request.Date:yyyy-MM-dd}",
                ct: innerCt);

        }, ct);

        return savedPayment;
    }

    public async Task UpdateNotesAsync(Guid paymentId, string? notes, CancellationToken ct = default)
    {
        await _paymentRepo.UpdateNotesAsync(paymentId, notes, ct);
    }

    /// <summary>
    /// Returns the GL account number of the money side. Card and electronic transfer need an
    /// existing bank account other than Petty Cash; any other method ignores <c>BankAccountId</c>.
    /// </summary>
    private async Task<string> ResolveMoneyAccountNumberAsync(RecordPaymentRequest request, CancellationToken ct)
    {
        switch (request.PaymentMethod)
        {
            case PaymentMethod.Cash:
                return SystemAccounts.CashNumber;
            case PaymentMethod.Check:
            case PaymentMethod.Other:
                return SystemAccounts.UndepositedFundsNumber;
        }

        var bank = request.BankAccountId is { } bankId && bankId != SystemAccounts.CashId
            ? await _accountRepo.GetByIdAsync(bankId, ct)
            : null;

        if (bank is null || !bank.IsBankAccount)
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_Deposit_BankAccountInvalid"),
                nameof(Payment), nameof(RecordAsync), request.BankAccountId);

        return bank.AccountNumber;
    }

    /// <summary>Trims the reference, returning null when blank (data-model §3).</summary>
    private static string? NormaliseReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        var trimmed = reference.Trim();
        return trimmed.Length > ReferenceMaxLength ? trimmed[..ReferenceMaxLength] : trimmed;
    }
}
