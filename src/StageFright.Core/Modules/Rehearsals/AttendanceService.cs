using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Modules.Rehearsals;

/// <summary>
/// Records batch attendance for a rehearsal in a single atomic transaction.
/// GL pair logic:
///   Accrual: Debit MemberReceivable (1200) / Credit first-available Income account.
///   Payment: Debit Cash (1100) / Credit MemberReceivable (1200) — only when PaidAtCreation=true.
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
    private readonly ILocalizer _localizer;

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
        IRehearsalService rehearsalService,
        ILocalizer localizer)
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
        _localizer = localizer;
    }

    public async Task<IReadOnlyList<AttendanceRecord>> GetByRehearsalAsync(Guid rehearsalId, CancellationToken ct = default)
    {
        return await _attendanceRepo.GetByRehearsalAsync(rehearsalId, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, bool>> GetPaidStatusByRehearsalAsync(Guid rehearsalId, CancellationToken ct = default)
    {
        var fees = await _feeRepo.GetByRehearsalAsync(rehearsalId, ct);
        return fees
            .Where(f => f.FeeType == FeeType.Attendance)
            .ToDictionary(f => f.MemberId, f => f.PaidAtCreation);
    }

    public async Task RecordBatchAsync(Guid rehearsalId, IReadOnlyList<AttendanceBatchItem> items, CancellationToken ct = default)
    {
        var rehearsal = await _rehearsalRepo.GetByIdAsync(rehearsalId, ct)
            ?? throw new EntityNotFoundException("Rehearsal", rehearsalId, nameof(RecordBatchAsync));

        if (rehearsal.Date.Date > DateTime.Today)
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_Attendance_BeforeRehearsalDate"),
                "Rehearsal", nameof(RecordBatchAsync), rehearsalId);

        var settings = await _settingsRepo.GetAsync(ct)
            ?? throw new ValidationException(_localizer.Get<ValidationResource>("Validation_Settings_NotConfigured"), "Settings", nameof(RecordBatchAsync));

        // Resolve income account once for the whole batch
        var accounts = await _accountRepo.GetAllAsync(ct);
        var incomeAccount = accounts.FirstOrDefault(c => c.Type == AccountType.Income && !c.IsSystem)
            ?? throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_Attendance_NoIncomeAccount"),
                "Account", nameof(RecordBatchAsync));

        // Per-fee-type tax treatment, stamped on each Fee at accrual. Tax is recognised
        // at accrual only — the payment pair below always clears the gross receivable.
        var taxCode = settings.IsTaxApplicable
            ? settings.AttendanceFeeTaxCode ?? TaxCode.TaxExempt
            : (TaxCode?)null;
        // Under Exclusive entry mode settings.AttendanceFee is the net and tax is added on top;
        // under Inclusive it is the gross and tax is split back out. Fee.Amount, the receivable
        // and the paid-at-creation payment pair all carry the gross; the income line the net
        // (issue #354).
        var (grossAmount, incomeAmount, taxAmount) = taxCode == TaxCode.Taxable
            ? TaxCalculator.Split(settings.AttendanceFee, settings.TaxEntryMode, settings.TaxRate ?? 0m,
                CurrencyCatalog.Get(settings.CurrencyCode).MinorUnitDigits)
            : (settings.AttendanceFee, settings.AttendanceFee, 0m);

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
                    Amount = grossAmount,
                    FeeDate = rehearsal.Date,
                    DueDate = rehearsal.Date,
                    PaidAtCreation = paidAtCreation,
                    RehearsalId = rehearsalId,
                    TaxCode = taxCode,
                    CreatedAt = now
                };
                var savedFee = await _feeRepo.AddAsync(fee, innerCt);

                // GL accrual: Debit MemberReceivable gross / Credit Income net
                // (+ Credit Tax Collected when the fee is taxable while tax applies).
                var accrualLines = new List<Transaction>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Date = rehearsal.Date,
                        AccountId = SystemAccounts.MemberReceivableId,
                        DebitAmount = grossAmount,
                        CreditAmount = 0m,
                        GLAccount = SystemAccounts.MemberReceivableNumber,
                        MemberId = item.MemberId,
                        FeeId = savedFee.Id,
                        TaxCode = taxCode,
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
                        TaxCode = taxCode,
                        Description = "Attendance fee income",
                        CreatedAt = now
                    }
                };

                if (taxAmount != 0m)
                {
                    accrualLines.Add(new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = rehearsal.Date,
                        AccountId = SystemAccounts.TaxCollectedId,
                        DebitAmount = 0m,
                        CreditAmount = taxAmount,
                        GLAccount = SystemAccounts.TaxCollectedNumber,
                        MemberId = item.MemberId,
                        FeeId = savedFee.Id,
                        TaxCode = taxCode,
                        Description = "Tax collected — attendance fee",
                        CreatedAt = now
                    });
                }

                await _glRepo.AddBalancedSetAsync(accrualLines, innerCt);

                // Audit the fee accrual (spec 028, US8 / FR-026) — every financial posting path
                // leaves an audit-trail entry, written inside this same transaction so a rollback
                // takes the audit row with it.
                await _audit.LogAsync(
                    nameof(Fee), savedFee.Id, AuditAction.Create,
                    oldValue: null,
                    newValue: $"Attendance fee {settings.AttendanceFee:C} accrued for member {item.MemberId} (rehearsal {rehearsalId})",
                    ct: innerCt);

                if (paidAtCreation)
                {
                    // Auto-create Payment record
                    var payment = new Payment
                    {
                        Id = Guid.NewGuid(),
                        MemberId = item.MemberId,
                        Date = rehearsal.Date,
                        Amount = grossAmount,
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
                            DebitAmount = grossAmount,
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
                            CreditAmount = grossAmount,
                            GLAccount = SystemAccounts.MemberReceivableNumber,
                            MemberId = item.MemberId,
                            PaymentId = savedPayment.Id,
                            FeeId = savedFee.Id,
                            Description = "Attendance fee receivable cleared",
                            CreatedAt = now
                        },
                        innerCt);

                    // Audit the automatic payment too (spec 028, US8 / FR-026).
                    await _audit.LogAsync(
                        nameof(Payment), savedPayment.Id, AuditAction.Create,
                        oldValue: null,
                        newValue: $"Attendance fee payment {settings.AttendanceFee:C} from member {item.MemberId} on {rehearsal.Date:yyyy-MM-dd}",
                        ct: innerCt);
                }
            }

            if (attendanceRecords.Count > 0)
                await _attendanceRepo.AddBatchAsync(attendanceRecords, innerCt);

        }, ct);

        // Freeze attendance rate after the transaction commits
        await _rehearsalService.FreezeAttendanceRateAsync(rehearsalId, rehearsal.Date, presentCount, ct);
    }
}
