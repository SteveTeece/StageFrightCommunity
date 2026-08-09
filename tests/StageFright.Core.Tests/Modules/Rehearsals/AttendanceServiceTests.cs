using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
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
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly ISettingsRepository _settingsRepo = Substitute.For<ISettingsRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRehearsalService _rehearsalService = Substitute.For<IRehearsalService>();

    private static readonly Guid RehearsalId = Guid.NewGuid();
    private static readonly Guid ActiveMemberId = Guid.NewGuid();
    private static readonly Guid InactiveMemberId = Guid.NewGuid();

    // System account GUIDs as seeded in StageFrightDbContext
    private static readonly Guid CashAccountId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid MemberReceivableAccountId = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid IncomeAccountId = Guid.NewGuid();

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

        // Income account (non-system)
        var incomeAccount = new Account
        {
            Id = IncomeAccountId, Name = "Attendance Income",
            Type = AccountType.Income, AccountNumber = "4000",
            IsSystem = false, SortOrder = 0,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account> { incomeAccount });

        _attendanceRepo.ExistsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _feeRepo.AddAsync(Arg.Any<Fee>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Fee>(0));
        _paymentRepo.AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Payment>(0));
    }

    private void SetSettings(bool isGstRegistered, GstCode? attendanceFeeGstCode, decimal attendanceFee) =>
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new Settings
            {
                Id = Guid.NewGuid(), OrganizationName = "Test",
                AnnualFee = 50m, AttendanceFee = attendanceFee,
                MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
                MinimumMemberAge = 0, SchemaVersion = "1.1.0",
                IsGstRegistered = isGstRegistered, AttendanceFeeGstCode = attendanceFeeGstCode,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

    private AttendanceService CreateService() =>
        new(_rehearsalRepo, _attendanceRepo, _memberRepo, _feeRepo, _paymentRepo,
            _glRepo, _accountRepo, _settingsRepo, _audit, _unitOfWork, _rehearsalService);

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
        await _glRepo.Received().AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines.Any(t => t.DebitAmount > 0 && t.AccountId == MemberReceivableAccountId) &&
                lines.Any(t => t.CreditAmount > 0 && t.AccountId == IncomeAccountId)),
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
            Arg.Is<Transaction>(t => t.DebitAmount > 0 && t.AccountId == CashAccountId),
            Arg.Is<Transaction>(t => t.CreditAmount > 0 && t.AccountId == MemberReceivableAccountId),
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

        // Only the accrual set posts. No cash/MemberReceivable payment pair.
        await _glRepo.Received(1).AddBalancedSetAsync(Arg.Any<IReadOnlyList<Transaction>>(), Arg.Any<CancellationToken>());
        await _glRepo.DidNotReceive().AddPairAsync(Arg.Any<Transaction>(), Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
        await _paymentRepo.DidNotReceive().AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
    }

    // --- GST ---

    [Fact]
    public async Task RecordBatch_NotRegistered_StampsNullGstCode_OnFeeAndAccrualLines_EvenWhenGstCodeConfigured()
    {
        // Toggle-off byte-identical regression: unregistered orgs must always get the
        // pre-GST 2-line accrual with GstCode == null, regardless of AttendanceFeeGstCode.
        SetSettings(isGstRegistered: false, attendanceFeeGstCode: GstCode.Gst, attendanceFee: 11m);
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true, MarkAsUnpaid = true } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        await _feeRepo.Received(1).AddAsync(Arg.Is<Fee>(f => f.GstCode == null), Arg.Any<CancellationToken>());
        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines => lines.Count == 2 && lines.All(t => t.GstCode == null)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordBatch_RegisteredAndTaxable_Posts3LineAccrual_SplitsGstToClearing()
    {
        SetSettings(isGstRegistered: true, attendanceFeeGstCode: GstCode.Gst, attendanceFee: 11m);
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true, MarkAsUnpaid = true } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines.Count == 3
                && lines.Any(t => t.DebitAmount == 11m && t.AccountId == MemberReceivableAccountId)
                && lines.Any(t => t.CreditAmount == 10m && t.AccountId == IncomeAccountId)
                && lines.Any(t => t.CreditAmount == 1m && t.AccountId == SystemAccounts.GstCollectedId)
                && lines.All(t => t.GstCode == GstCode.Gst)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(GstCode.GstFree)]
    [InlineData(GstCode.InputTaxed)]
    public async Task RecordBatch_RegisteredButNotTaxable_Posts2LineAccrual_NoGstSplit(GstCode code)
    {
        SetSettings(isGstRegistered: true, attendanceFeeGstCode: code, attendanceFee: 10m);
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true, MarkAsUnpaid = true } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines.Count == 2 && lines.All(t => t.GstCode == code)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordBatch_RegisteredWithNoFeeGstCodeSet_DefaultsToGstFree()
    {
        SetSettings(isGstRegistered: true, attendanceFeeGstCode: null, attendanceFee: 10m);
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true, MarkAsUnpaid = true } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines => lines.All(t => t.GstCode == GstCode.GstFree)),
            Arg.Any<CancellationToken>());
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

    // --- Future-dated rehearsal ---

    [Fact]
    public async Task RecordBatch_RehearsalDateInFuture_ThrowsValidationException()
    {
        var futureRehearsalId = Guid.NewGuid();
        _rehearsalRepo.GetByIdAsync(futureRehearsalId, Arg.Any<CancellationToken>()).Returns(new Rehearsal
        {
            Id = futureRehearsalId,
            Date = DateTime.Today.AddDays(1),
            Time = TimeSpan.FromHours(19),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });

        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true } };

        await Assert.ThrowsAsync<ValidationException>(() => svc.RecordBatchAsync(futureRehearsalId, items, Ct));
        await _attendanceRepo.DidNotReceive().AddBatchAsync(Arg.Any<IReadOnlyList<AttendanceRecord>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordBatch_RehearsalDateIsToday_Succeeds()
    {
        var todayRehearsalId = Guid.NewGuid();
        _rehearsalRepo.GetByIdAsync(todayRehearsalId, Arg.Any<CancellationToken>()).Returns(new Rehearsal
        {
            Id = todayRehearsalId,
            Date = DateTime.Today,
            Time = TimeSpan.FromHours(19),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });

        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true } };

        await svc.RecordBatchAsync(todayRehearsalId, items, Ct);

        await _feeRepo.Received(1).AddAsync(Arg.Is<Fee>(f => f.MemberId == ActiveMemberId), Arg.Any<CancellationToken>());
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
        await _glRepo.DidNotReceive().AddBalancedSetAsync(Arg.Any<IReadOnlyList<Transaction>>(), Arg.Any<CancellationToken>());
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
        await _glRepo.DidNotReceive().AddBalancedSetAsync(Arg.Any<IReadOnlyList<Transaction>>(), Arg.Any<CancellationToken>());
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

    // --- GetPaidStatusByRehearsalAsync ---

    [Fact]
    public async Task GetPaidStatusByRehearsalAsync_ReturnsTruePerMember_WhenFeePaidAtCreation()
    {
        var paidFee = new Fee
        {
            Id = Guid.NewGuid(), MemberId = ActiveMemberId, FeeType = FeeType.Attendance,
            Amount = 10m, FeeDate = DateTime.UtcNow, DueDate = DateTime.UtcNow,
            PaidAtCreation = true, RehearsalId = RehearsalId, CreatedAt = DateTime.UtcNow
        };
        _feeRepo.GetByRehearsalAsync(RehearsalId, Arg.Any<CancellationToken>())
            .Returns(new List<Fee> { paidFee });
        var svc = CreateService();

        var result = await svc.GetPaidStatusByRehearsalAsync(RehearsalId, Ct);

        Assert.True(result[ActiveMemberId]);
    }

    [Fact]
    public async Task GetPaidStatusByRehearsalAsync_ReturnsFalsePerMember_WhenFeeMarkedUnpaidAtCreation()
    {
        // Regression for #281: the read-only attendance grid must reflect the true paid
        // status, not always show "Paid" like it did when it mirrored the Attended flag.
        var unpaidFee = new Fee
        {
            Id = Guid.NewGuid(), MemberId = ActiveMemberId, FeeType = FeeType.Attendance,
            Amount = 10m, FeeDate = DateTime.UtcNow, DueDate = DateTime.UtcNow,
            PaidAtCreation = false, RehearsalId = RehearsalId, CreatedAt = DateTime.UtcNow
        };
        _feeRepo.GetByRehearsalAsync(RehearsalId, Arg.Any<CancellationToken>())
            .Returns(new List<Fee> { unpaidFee });
        var svc = CreateService();

        var result = await svc.GetPaidStatusByRehearsalAsync(RehearsalId, Ct);

        Assert.False(result[ActiveMemberId]);
    }

    [Fact]
    public async Task GetPaidStatusByRehearsalAsync_OmitsMembersWithNoAttendanceFee()
    {
        _feeRepo.GetByRehearsalAsync(RehearsalId, Arg.Any<CancellationToken>())
            .Returns(new List<Fee>());
        var svc = CreateService();

        var result = await svc.GetPaidStatusByRehearsalAsync(RehearsalId, Ct);

        Assert.False(result.ContainsKey(InactiveMemberId));
    }

    // --- Helpers ---

    private static Member ActiveMember(Guid id) => new()
    {
        Id = id, FirstName = "Active", LastName = "Member", StreetAddress = "1 Test St",
        Status = MemberStatus.Active, ActivateDate = DateTime.UtcNow.Date,
        JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Member InactiveMember(Guid id) => new()
    {
        Id = id, FirstName = "Inactive", LastName = "Member", StreetAddress = "2 Test St",
        Status = MemberStatus.Inactive, InactivateDate = DateTime.UtcNow.Date,
        JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
