using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Records member payments with FIFO GL allocation.
/// Per fee in FIFO order: Debit Cash (1100) / Credit MemberReceivable (1200).
/// Overpayment (payment > balance): Debit Cash (1100) / Credit MemberReceivable (1200) — creates negative (credit) balance.
/// </summary>
public class PaymentService : IPaymentService
{


    private readonly IFeeRepository _feeRepo;
    private readonly IPaymentRepository _paymentRepo;
    private readonly IGLRepository _glRepo;
    private readonly IMemberRepository _memberRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizer _localizer;

    public PaymentService(
        IFeeRepository feeRepo,
        IPaymentRepository paymentRepo,
        IGLRepository glRepo,
        IMemberRepository memberRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork,
        ILocalizer localizer)
    {
        _feeRepo = feeRepo;
        _paymentRepo = paymentRepo;
        _glRepo = glRepo;
        _memberRepo = memberRepo;
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
                            AccountId = SystemAccounts.CashId,
                            DebitAmount = allocation,
                            CreditAmount = 0m,
                            GLAccount = SystemAccounts.CashNumber,
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
                        AccountId = SystemAccounts.CashId,
                        DebitAmount = remainingPayment,
                        CreditAmount = 0m,
                        GLAccount = SystemAccounts.CashNumber,
                        MemberId = request.MemberId,
                        PaymentId = savedPayment.Id,
                        Description = $"Overpayment — cash received from {memberName}",
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

            // 4. Audit
            await _audit.LogAsync(
                nameof(Payment), savedPayment.Id, AuditAction.Create,
                oldValue: null,
                newValue: $"Payment {request.Amount:C} from member {request.MemberId} on {request.Date:yyyy-MM-dd}",
                ct: innerCt);

        }, ct);

        return savedPayment;
    }

    public async Task UpdateNotesAsync(Guid paymentId, string? notes, CancellationToken ct = default)
    {
        await _paymentRepo.UpdateNotesAsync(paymentId, notes, ct);
    }
}
