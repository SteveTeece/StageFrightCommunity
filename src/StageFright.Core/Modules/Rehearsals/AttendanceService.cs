using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Rehearsals;

/// <summary>
/// Records batch attendance for a rehearsal in a single atomic transaction.
/// GL pair logic:
///   Accrual: Debit MemberReceivable (0101) / Credit first-available Income category.
///   Payment: Debit Cash (0100) / Credit MemberReceivable (0101) — only when PaidAtCreation=true.
/// System category GUIDs are the seeded fixed values from StageFrightDbContext.
/// </summary>
public class AttendanceService : IAttendanceService
{
    private static readonly Guid CashCategoryId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid MemberReceivableCategoryId = new("00000000-0000-0000-0000-000000000002");

    private const string CashGLAccount = "0100";
    private const string MemberReceivableGLAccount = "0101";

    private readonly IRehearsalRepository _rehearsalRepo;
    private readonly IAttendanceRepository _attendanceRepo;
    private readonly IMemberRepository _memberRepo;
    private readonly IFeeRepository _feeRepo;
    private readonly IPaymentRepository _paymentRepo;
    private readonly IGLRepository _glRepo;
    private readonly ICategoryRepository _categoryRepo;
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
        ICategoryRepository categoryRepo,
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
        _categoryRepo = categoryRepo;
        _settingsRepo = settingsRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _rehearsalService = rehearsalService;
    }

    public async Task RecordBatchAsync(Guid rehearsalId, IReadOnlyList<AttendanceBatchItem> items, CancellationToken ct = default)
    {
        var rehearsal = await _rehearsalRepo.GetByIdAsync(rehearsalId, ct)
            ?? throw new EntityNotFoundException("Rehearsal", rehearsalId, nameof(RecordBatchAsync));

        var settings = await _settingsRepo.GetAsync(ct)
            ?? throw new ValidationException("Application settings are not configured.", "Settings", nameof(RecordBatchAsync));

        // Resolve income category once for the whole batch
        var categories = await _categoryRepo.GetAllAsync(ct);
        var incomeCategory = categories.FirstOrDefault(c => c.Type == CategoryType.Income && !c.IsSystem)
            ?? throw new ValidationException(
                "No income category configured. Please set up categories in Settings before recording attendance.",
                "Category", nameof(RecordBatchAsync));

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
                    CreatedAt = now
                };
                var savedFee = await _feeRepo.AddAsync(fee, innerCt);

                // GL accrual pair: Debit MemberReceivable / Credit Income
                await _glRepo.AddPairAsync(
                    new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = rehearsal.Date,
                        CategoryId = MemberReceivableCategoryId,
                        DebitAmount = settings.AttendanceFee,
                        CreditAmount = 0m,
                        GLAccount = MemberReceivableGLAccount,
                        MemberId = item.MemberId,
                        FeeId = savedFee.Id,
                        Description = "Attendance fee accrual",
                        CreatedAt = now
                    },
                    new Transaction
                    {
                        Id = Guid.NewGuid(),
                        Date = rehearsal.Date,
                        CategoryId = incomeCategory.Id,
                        DebitAmount = 0m,
                        CreditAmount = settings.AttendanceFee,
                        GLAccount = incomeCategory.GLAccount,
                        MemberId = item.MemberId,
                        FeeId = savedFee.Id,
                        Description = "Attendance fee income",
                        CreatedAt = now
                    },
                    innerCt);

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
                            CategoryId = CashCategoryId,
                            DebitAmount = settings.AttendanceFee,
                            CreditAmount = 0m,
                            GLAccount = CashGLAccount,
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
                            CategoryId = MemberReceivableCategoryId,
                            DebitAmount = 0m,
                            CreditAmount = settings.AttendanceFee,
                            GLAccount = MemberReceivableGLAccount,
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
