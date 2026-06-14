using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Records member payments with FIFO GL allocation.
/// Per fee in FIFO order: Debit Cash (0100) / Credit MemberReceivable (0101).
/// Overpayment (payment > balance): Debit MemberReceivable (0101) / Credit Cash (0100).
/// </summary>
public class PaymentService : IPaymentService
{
    private static readonly Guid CashCategoryId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid MemberReceivableCategoryId = new("00000000-0000-0000-0000-000000000002");

    private const string CashGLAccount = "0100";
    private const string MemberReceivableGLAccount = "0101";

    private readonly IFeeRepository _feeRepo;
    private readonly IPaymentRepository _paymentRepo;
    private readonly IGLRepository _glRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public PaymentService(
        IFeeRepository feeRepo,
        IPaymentRepository paymentRepo,
        IGLRepository glRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _feeRepo = feeRepo;
        _paymentRepo = paymentRepo;
        _glRepo = glRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<Payment> RecordAsync(RecordPaymentRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0m)
            throw new ValidationException(
                "Payment amount must be greater than zero.", nameof(Payment), nameof(RecordAsync));

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

            // 2. FIFO allocation: get outstanding balance and fees in order
            var outstandingBalance = await _glRepo.GetMemberBalanceAsync(request.MemberId, innerCt);
            var fees = await _feeRepo.GetUnpaidOrderedFifoAsync(request.MemberId, innerCt);

            decimal remainingPayment = request.Amount;

            if (outstandingBalance > 0m && fees.Count > 0)
            {
                // Allocate against fees in FIFO order (oldest first)
                foreach (var fee in fees)
                {
                    if (remainingPayment <= 0m)
                        break;

                    // Amount to apply to this fee: limited by payment remaining and fee face value
                    decimal allocation = Math.Min(remainingPayment, fee.Amount);

                    await _glRepo.AddPairAsync(
                        new Transaction
                        {
                            Id = Guid.NewGuid(),
                            Date = request.Date,
                            CategoryId = CashCategoryId,
                            DebitAmount = allocation,
                            CreditAmount = 0m,
                            GLAccount = CashGLAccount,
                            MemberId = request.MemberId,
                            PaymentId = savedPayment.Id,
                            FeeId = fee.Id,
                            Description = $"Payment allocation against fee {fee.Id}",
                            CreatedAt = now
                        },
                        new Transaction
                        {
                            Id = Guid.NewGuid(),
                            Date = request.Date,
                            CategoryId = MemberReceivableCategoryId,
                            CreditAmount = allocation,
                            DebitAmount = 0m,
                            GLAccount = MemberReceivableGLAccount,
                            MemberId = request.MemberId,
                            PaymentId = savedPayment.Id,
                            FeeId = fee.Id,
                            Description = $"Payment received — receivable cleared for fee {fee.Id}",
                            CreatedAt = now
                        },
                        innerCt);

                    remainingPayment -= allocation;
                }
            }

            // 3. Overpayment: record credit balance for member
            if (remainingPayment > 0m)
            {
                await _glRepo.AddPairAsync(
                    new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = request.Date,
                        CategoryId = MemberReceivableCategoryId,
                        DebitAmount = remainingPayment,
                        CreditAmount = 0m,
                        GLAccount = MemberReceivableGLAccount,
                        MemberId = request.MemberId,
                        PaymentId = savedPayment.Id,
                        Description = "Overpayment credit to member account",
                        CreatedAt = now
                    },
                    new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = request.Date,
                        CategoryId = CashCategoryId,
                        DebitAmount = 0m,
                        CreditAmount = remainingPayment,
                        GLAccount = CashGLAccount,
                        MemberId = request.MemberId,
                        PaymentId = savedPayment.Id,
                        Description = "Overpayment credit — cash returned to member",
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
