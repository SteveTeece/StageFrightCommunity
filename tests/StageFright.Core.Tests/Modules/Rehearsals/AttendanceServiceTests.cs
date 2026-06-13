using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Rehearsals;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Rehearsals;

/// <summary>
/// Unit tests for AttendanceService — fee creation, GL pairs, Payment auto-creation, idempotency, batch atomicity.
/// </summary>
public class AttendanceServiceTests : TestBase
{
    private readonly IRehearsalRepository _rehearsalRepo = Substitute.For<IRehearsalRepository>();
    private readonly IAttendanceRepository _attendanceRepo = Substitute.For<IAttendanceRepository>();
    private readonly IMemberRepository _memberRepo = Substitute.For<IMemberRepository>();
    private readonly IFeeRepository _feeRepo = Substitute.For<IFeeRepository>();
    private readonly IPaymentRepository _paymentRepo = Substitute.For<IPaymentRepository>();
    private readonly IGLRepository _glRepo = Substitute.For<IGLRepository>();
    private readonly ICategoryRepository _categoryRepo = Substitute.For<ICategoryRepository>();
    private readonly ISettingsRepository _settingsRepo = Substitute.For<ISettingsRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRehearsalService _rehearsalService = Substitute.For<IRehearsalService>();

    private static readonly Guid RehearsalId = Guid.NewGuid();
    private static readonly Guid ActiveMemberId = Guid.NewGuid();
    private static readonly Guid InactiveMemberId = Guid.NewGuid();

    // System category GUIDs as seeded in StageFrightDbContext
    private static readonly Guid CashCategoryId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid MemberReceivableCategoryId = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid IncomeCategoryId = Guid.NewGuid();

    public AttendanceServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new Settings
            {
                Id = Guid.NewGuid(), OrganizationName = "Test",
                AnnualFee = 50m, AttendanceFee = 10m,
                MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
                MinimumMemberAge = 0, SchemaVersion = "1.0.0",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        var rehearsal = new Rehearsal
        {
            Id = RehearsalId,
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Time = TimeSpan.FromHours(19),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _rehearsalRepo.GetByIdAsync(RehearsalId, Arg.Any<CancellationToken>()).Returns(rehearsal);

        _memberRepo.GetByIdAsync(ActiveMemberId, Arg.Any<CancellationToken>())
            .Returns(ActiveMember(ActiveMemberId));
        _memberRepo.GetByIdAsync(InactiveMemberId, Arg.Any<CancellationToken>())
            .Returns(InactiveMember(InactiveMemberId));

        // Income category (non-system)
        var incomeCategory = new Category
        {
            Id = IncomeCategoryId, Name = "Attendance Income",
            Type = CategoryType.Income, GLAccount = "1000",
            IsSystem = false, SortOrder = 0,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _categoryRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Category> { incomeCategory });

        _attendanceRepo.ExistsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _feeRepo.AddAsync(Arg.Any<Fee>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Fee>(0));
        _paymentRepo.AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Payment>(0));
    }

    private AttendanceService CreateService() =>
        new(_rehearsalRepo, _attendanceRepo, _memberRepo, _feeRepo, _paymentRepo,
            _glRepo, _categoryRepo, _settingsRepo, _audit, _unitOfWork, _rehearsalService);

    // --- Attended + active + PaidAtCreation (default) ---

    [Fact]
    public async Task RecordBatch_ActiveMemberAttended_CreatesFeeWithPaidAtCreationTrue()
    {
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true, MarkAsUnpaid = false } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        await _feeRepo.Received(1).AddAsync(
            Arg.Is<Fee>(f => f.MemberId == ActiveMemberId && f.PaidAtCreation == true && f.FeeType == FeeType.Attendance),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordBatch_ActiveMemberAttended_CreatesGLAccrualPair()
    {
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true, MarkAsUnpaid = false } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        // Accrual: Debit MemberReceivable / Credit Income — must be called at least once
        await _glRepo.Received().AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount > 0 && t.CategoryId == MemberReceivableCategoryId),
            Arg.Is<Transaction>(t => t.CreditAmount > 0 && t.CategoryId == IncomeCategoryId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordBatch_ActiveMemberAttended_PaidAtCreation_CreatesGLPaymentPair()
    {
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true, MarkAsUnpaid = false } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        // Payment: Debit Cash / Credit MemberReceivable
        await _glRepo.Received().AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount > 0 && t.CategoryId == CashCategoryId),
            Arg.Is<Transaction>(t => t.CreditAmount > 0 && t.CategoryId == MemberReceivableCategoryId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordBatch_ActiveMemberAttended_PaidAtCreation_CreatesPaymentRecord()
    {
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true, MarkAsUnpaid = false } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        await _paymentRepo.Received(1).AddAsync(
            Arg.Is<Payment>(p =>
                p.MemberId == ActiveMemberId &&
                p.PaymentMethod == PaymentMethod.Cash &&
                p.PaymentType == PaymentType.Attendance &&
                p.Amount == 10m),
            Arg.Any<CancellationToken>());
    }

    // --- Mark as unpaid ---

    [Fact]
    public async Task RecordBatch_MarkAsUnpaid_CreatesFeeWithPaidAtCreationFalse()
    {
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true, MarkAsUnpaid = true } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        await _feeRepo.Received(1).AddAsync(
            Arg.Is<Fee>(f => f.PaidAtCreation == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordBatch_MarkAsUnpaid_CreatesOnlyAccrualGLPair_NoPaymentPair()
    {
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true, MarkAsUnpaid = true } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        // Only one GL pair: accrual. No cash/MemberReceivable payment pair.
        await _glRepo.Received(1).AddPairAsync(Arg.Any<Transaction>(), Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
        await _paymentRepo.DidNotReceive().AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
    }

    // --- Inactive member ---

    [Fact]
    public async Task RecordBatch_InactiveMember_CreatesAttendanceRecord_ButNoFee()
    {
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = InactiveMemberId, Attended = true, MarkAsUnpaid = false } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        await _attendanceRepo.Received().AddBatchAsync(
            Arg.Is<IReadOnlyList<AttendanceRecord>>(list => list.Any(r => r.MemberId == InactiveMemberId)),
            Arg.Any<CancellationToken>());
        await _feeRepo.DidNotReceive().AddAsync(Arg.Any<Fee>(), Arg.Any<CancellationToken>());
    }

    // --- Not attended ---

    [Fact]
    public async Task RecordBatch_NotAttended_NoFeeAndNoGLPair()
    {
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = false, MarkAsUnpaid = false } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        await _feeRepo.DidNotReceive().AddAsync(Arg.Any<Fee>(), Arg.Any<CancellationToken>());
        await _glRepo.DidNotReceive().AddPairAsync(Arg.Any<Transaction>(), Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
    }

    // --- Idempotency ---

    [Fact]
    public async Task RecordBatch_DuplicateAttendance_IsIdempotent()
    {
        _attendanceRepo.ExistsAsync(RehearsalId, ActiveMemberId, Arg.Any<CancellationToken>())
            .Returns(true); // Already recorded

        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        await _feeRepo.DidNotReceive().AddAsync(Arg.Any<Fee>(), Arg.Any<CancellationToken>());
        await _glRepo.DidNotReceive().AddPairAsync(Arg.Any<Transaction>(), Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
    }

    // --- Batch atomicity ---

    [Fact]
    public async Task RecordBatch_RunsInsideUnitOfWork()
    {
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    // --- Rate freeze called ---

    [Fact]
    public async Task RecordBatch_CallsFreezeAttendanceRate_AfterSave()
    {
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        await _rehearsalService.Received(1).FreezeAttendanceRateAsync(
            RehearsalId, Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private static Member ActiveMember(Guid id) => new()
    {
        Id = id, Name = "Active", StreetAddress = "1 Test St",
        Status = MemberStatus.Active, ActivateDate = DateTime.UtcNow.Date,
        JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Member InactiveMember(Guid id) => new()
    {
        Id = id, Name = "Inactive", StreetAddress = "2 Test St",
        Status = MemberStatus.Inactive, InactivateDate = DateTime.UtcNow.Date,
        JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
