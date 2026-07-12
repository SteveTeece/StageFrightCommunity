using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Modules.Rehearsals;

/// <summary>
/// Records batch attendance for a rehearsal in a single atomic transaction.
/// GL pair logic:
///   Accrual: Debit MemberReceivable (0101) / Credit first-available Income account.
///   Payment: Debit Cash (0100) / Credit MemberReceivable (0101) — only when PaidAtCreation=true.
/// System account GUIDs are the seeded fixed values from StageFrightDbContext.
/// </summary>
public class AttendanceService : IAttendanceService
{


    private readonly IRehearsalRepository _rehearsalRepo;
    private readonly IAttendanceRepository _attendanceRepo;
    private readonly IMemberRepository _memberRepo;
    private readonly IFeeRepository _feeRepo;
    private readonly IPaymentRepository _paymentRepo;
    private readonly IGLRepository _glRepo;
    private readonly IAccountRepository _accountRepo;
    private readonly ISettingsRepository _settingsRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRehearsalService _rehearsalService;

    public AttendanceService(
        IRehearsalRepository rehearsalRepo,
        IAttendanceRepository attendanceRepo,
        IMemberRepository memberRepo,
        IFeeRepository feeRepo,
        IPaymentRepository paymentRepo,
        IGLRepository glRepo,
        IAccountRepository accountRepo,
        ISettingsRepository settingsRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork,
        IRehearsalService rehearsalService)
    {
        _rehearsalRepo = rehearsalRepo;
        _attendanceRepo = attendanceRepo;
        _memberRepo = memberRepo;
        _feeRepo = feeRepo;
        _paymentRepo = paymentRepo;
        _glRepo = glRepo;
        _accountRepo = accountRepo;
        _settingsRepo = settingsRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _rehearsalService = rehearsalService;
    }

    public async Task<IReadOnlyList<AttendanceRecord>> GetByRehearsalAsync(Guid rehearsalId, CancellationToken ct = default)
    {
        return await _attendanceRepo.GetByRehearsalAsync(rehearsalId, ct);
    }

    public async Task RecordBatchAsync(Guid rehearsalId, IReadOnlyList<AttendanceBatchItem> items, CancellationToken ct = default)
    {
        var rehearsal = await _rehearsalRepo.GetByIdAsync(rehearsalId, ct)
            ?? throw new EntityNotFoundException("Rehearsal", rehearsalId, nameof(RecordBatchAsync));

        if (rehearsal.Date.Date > DateTime.UtcNow.Date)
            throw new ValidationException(
                "Attendance cannot be recorded before the rehearsal date.",
                "Rehearsal", nameof(RecordBatchAsync), rehearsalId);

        var settings = await _settingsRepo.GetAsync(ct)
            ?? throw new ValidationException("Application settings are not configured.", "Settings", nameof(RecordBatchAsync));

        // Resolve income account once for the whole batch
        var accounts = await _accountRepo.GetAllAsync(ct);
        var incomeAccount = accounts.FirstOrDefault(c => c.Type == AccountType.Income && !c.IsSystem)
            ?? throw new ValidationException(
                "No income account configured. Please set up accounts in Settings before recording attendance.",
                "Account", nameof(RecordBatchAsync));

        // Per-fee-type GST treatment, stamped on each Fee at accrual. GST is recognised
        // at accrual only — the payment pair below always clears the gross receivable.
        var gstCode = settings.IsGstRegistered
            ? settings.AttendanceFeeGstCode ?? GstCode.GstFree
            : (GstCode?)null;
        var (incomeAmount, gstAmount) = gstCode == GstCode.Gst
            ? GstCalculator.SplitInclusive(settings.AttendanceFee)
            : (settings.AttendanceFee, 0m);

        int presentCount = 0;
        var attendanceRecords = new List<AttendanceRecord>();

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            foreach (var item in items)
            {
                // Idempotency: skip if already recorded
                if (await _attendanceRepo.ExistsAsync(rehearsalId, item.MemberId, innerCt))
                    continue;

                var member = await _memberRepo.GetByIdAsync(item.MemberId, innerCt);
                if (member is null)
                    continue;

                attendanceRecords.Add(new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    RehearsalId = rehearsalId,
                    MemberId = item.MemberId,
                    Attended = item.Attended,
                    CreatedAt = DateTime.UtcNow
                });

                if (!item.Attended)
                    continue;

                if (member.Status != MemberStatus.Active)
                    continue;

                presentCount++;

                bool paidAtCreation = !item.MarkAsUnpaid;
                var now = DateTime.UtcNow;

                // Create attendance fee
                var fee = new Fee
                {
                    Id = Guid.NewGuid(),
                    MemberId = item.MemberId,
                    FeeType = FeeType.Attendance,
                    Amount = settings.AttendanceFee,
                    FeeDate = rehearsal.Date,
                    DueDate = rehearsal.Date,
                    PaidAtCreation = paidAtCreation,
                    RehearsalId = rehearsalId,
                    GstCode = gstCode,
                    CreatedAt = now
                };
                var savedFee = await _feeRepo.AddAsync(fee, innerCt);

                // GL accrual: Debit MemberReceivable gross / Credit Income net
                // (+ Credit GST Collected when the fee is taxable while registered).
                var accrualLines = new List<Transaction>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Date = rehearsal.Date,
                        AccountId = SystemAccounts.MemberReceivableId,
                        DebitAmount = settings.AttendanceFee,
                        CreditAmount = 0m,
                        GLAccount = SystemAccounts.MemberReceivableNumber,
                        MemberId = item.MemberId,
                        FeeId = savedFee.Id,
                        GstCode = gstCode,
                        Description = "Attendance fee accrual",
                        CreatedAt = now
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Date = rehearsal.Date,
                        AccountId = incomeAccount.Id,
                        DebitAmount = 0m,
                        CreditAmount = incomeAmount,
                        GLAccount = incomeAccount.AccountNumber,
                        MemberId = item.MemberId,
                        FeeId = savedFee.Id,
                        GstCode = gstCode,
                        Description = "Attendance fee income",
                        CreatedAt = now
                    }
                };

                if (gstAmount != 0m)
                {
                    accrualLines.Add(new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = rehearsal.Date,
                        AccountId = SystemAccounts.GstCollectedId,
                        DebitAmount = 0m,
                        CreditAmount = gstAmount,
                        GLAccount = SystemAccounts.GstCollectedNumber,
                        MemberId = item.MemberId,
                        FeeId = savedFee.Id,
                        GstCode = gstCode,
                        Description = "GST collected — attendance fee",
                        CreatedAt = now
                    });
                }

                await _glRepo.AddBalancedSetAsync(accrualLines, innerCt);

                if (paidAtCreation)
                {
                    // Auto-create Payment record
                    var payment = new Payment
                    {
                        Id = Guid.NewGuid(),
                        MemberId = item.MemberId,
                        Date = rehearsal.Date,
                        Amount = settings.AttendanceFee,
                        PaymentMethod = PaymentMethod.Cash,
                        PaymentType = PaymentType.Attendance,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    var savedPayment = await _paymentRepo.AddAsync(payment, innerCt);

                    // GL payment pair: Debit Cash / Credit MemberReceivable
                    await _glRepo.AddPairAsync(
                        new Transaction
                        {
                            Id = Guid.NewGuid(),
                            Date = rehearsal.Date,
                            AccountId = SystemAccounts.CashId,
                            DebitAmount = settings.AttendanceFee,
                            CreditAmount = 0m,
                            GLAccount = SystemAccounts.CashNumber,
                            MemberId = item.MemberId,
                            PaymentId = savedPayment.Id,
                            FeeId = savedFee.Id,
                            Description = "Attendance fee payment (cash)",
                            CreatedAt = now
                        },
                        new Transaction
                        {
                            Id = Guid.NewGuid(),
                            Date = rehearsal.Date,
                            AccountId = SystemAccounts.MemberReceivableId,
                            DebitAmount = 0m,
                            CreditAmount = settings.AttendanceFee,
                            GLAccount = SystemAccounts.MemberReceivableNumber,
                            MemberId = item.MemberId,
                            PaymentId = savedPayment.Id,
                            FeeId = savedFee.Id,
                            Description = "Attendance fee receivable cleared",
                            CreatedAt = now
                        },
                        innerCt);
                }
            }

            if (attendanceRecords.Count > 0)
                await _attendanceRepo.AddBatchAsync(attendanceRecords, innerCt);

        }, ct);

        // Freeze attendance rate after the transaction commits
        await _rehearsalService.FreezeAttendanceRateAsync(rehearsalId, rehearsal.Date, presentCount, ct);
    }
}
