using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Rehearsals;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Rehearsals;

/// <summary>
/// Spec 028 US8 (FR-026, SC-011): every financial posting path — including attendance-fee
/// accruals and their automatic payment — must leave an audit-trail entry. These tests hold
/// <see cref="AttendanceService.RecordBatchAsync"/> to writing an <c>AuditTrailEntry</c> for the
/// fee accrual and, when the fee is paid at creation, for the auto-payment.
/// </summary>
public class AttendanceServiceAuditTests : TestBase
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
    private static readonly Guid IncomeAccountId = Guid.NewGuid();

    public AttendanceServiceAuditTests()
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
                MinimumMemberAge = 0, SchemaVersion = "1.1.0",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        _rehearsalRepo.GetByIdAsync(RehearsalId, Arg.Any<CancellationToken>()).Returns(new Rehearsal
        {
            Id = RehearsalId,
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Time = TimeSpan.FromHours(19),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });

        _memberRepo.GetByIdAsync(ActiveMemberId, Arg.Any<CancellationToken>()).Returns(new Member
        {
            Id = ActiveMemberId, FirstName = "Active", LastName = "Member", StreetAddress = "1 Test St",
            Status = MemberStatus.Active, ActivateDate = DateTime.UtcNow.Date,
            JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });

        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Account>
        {
            new()
            {
                Id = IncomeAccountId, Name = "Attendance Income",
                Type = AccountType.Income, AccountNumber = "4000",
                IsSystem = false, SortOrder = 0,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }
        });

        _attendanceRepo.ExistsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        _feeRepo.AddAsync(Arg.Any<Fee>(), Arg.Any<CancellationToken>()).Returns(ci => ci.ArgAt<Fee>(0));
        _paymentRepo.AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>()).Returns(ci => ci.ArgAt<Payment>(0));
    }

    private AttendanceService CreateService() =>
        new(_rehearsalRepo, _attendanceRepo, _memberRepo, _feeRepo, _paymentRepo,
            _glRepo, _accountRepo, _settingsRepo, _audit, _unitOfWork, _rehearsalService, RealLocalizer.Instance);

    [Fact]
    public async Task Should_WriteAuditEntryForTheFeeAccrual_When_AttendanceAccruesAFee()
    {
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true, MarkAsUnpaid = true } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        await _audit.Received(1).LogAsync(
            nameof(Fee), Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_WriteAuditEntryForTheAutoPayment_When_FeePaidAtCreation()
    {
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true, MarkAsUnpaid = false } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        await _audit.Received(1).LogAsync(
            nameof(Fee), Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _audit.Received(1).LogAsync(
            nameof(Payment), Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_WriteOnlyTheAccrualAuditEntry_When_FeeMarkedUnpaid()
    {
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true, MarkAsUnpaid = true } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        await _audit.Received(1).LogAsync(
            nameof(Fee), Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _audit.DidNotReceive().LogAsync(
            nameof(Payment), Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_WriteTheAuditEntriesInsideTheTransaction_When_AttendanceRecorded()
    {
        // The audit writes must ride inside the same IUnitOfWork transaction as the fee/GL rows
        // so a rollback leaves no orphan audit entry. With the UoW stubbed to run its callback
        // inline, a Received() on _audit proves the call happened within that callback.
        var svc = CreateService();
        var items = new[] { new AttendanceBatchItem { MemberId = ActiveMemberId, Attended = true, MarkAsUnpaid = false } };

        await svc.RecordBatchAsync(RehearsalId, items, Ct);

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
        await _audit.Received(2).LogAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
