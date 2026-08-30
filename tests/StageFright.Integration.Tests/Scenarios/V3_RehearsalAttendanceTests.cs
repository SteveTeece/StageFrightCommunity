using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Members;
using StageFright.Core.Modules.Rehearsals;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V3: rehearsal scheduling and attendance recording.
/// Verifies Fee creation, GL pairs, Payment auto-creation, StoredAttendanceRate freezing, and idempotency.
/// Uses a real SQLite in-memory database with full EF migrations.
/// </summary>
public sealed class V3_RehearsalAttendanceTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    // System account GUIDs (seeded)
    private static readonly Guid CashAccountId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid MemberReceivableAccountId = new("00000000-0000-0000-0000-000000000002");

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        // Seed an income account for GL pairs
        _db.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(),
            Name = "Attendance Income",
            Type = AccountType.Income,
            AccountNumber = "1000",
            SortOrder = 0,
            IsSystem = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Seed settings
        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(),
            OrganizationName = "Test Choir",
            AnnualFee = 50m,
            AttendanceFee = 10m,
            MembershipRenewalMonth = 1,
            CommitteeRenewalMonth = 1,
            MaxAgeRangeYears = 150,
            MinimumMemberAge = 0,
            Theme = Theme.Light,
            SchemaVersion = "1.0.0",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- Schedule rehearsal ---

    [Fact]
    public async Task ScheduleRehearsal_Persists_WithNullStoredAttendanceRate()
    {
        var rehearsalSvc = BuildRehearsalService();

        var rehearsal = await rehearsalSvc.ScheduleAsync(new ScheduleRehearsalRequest
        {
            Date = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            Time = TimeSpan.FromHours(19),
            Notes = "Summer session"
        }, TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, rehearsal.Id);
        Assert.Null(rehearsal.StoredAttendanceRate);

        var found = await new RehearsalRepository(_db).GetByIdAsync(rehearsal.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(found);
        Assert.Null(found!.StoredAttendanceRate);
    }

    // --- Batch attendance: attended active member, paid at creation ---

    [Fact]
    public async Task RecordBatch_AttendedActiveMember_PaidAtCreation_CreatesFeeAndGLPairsAndPayment()
    {
        var member = await AddActiveMember("John");
        var rehearsal = await ScheduleRehearsal();

        var attendanceSvc = BuildAttendanceService();
        await attendanceSvc.RecordBatchAsync(rehearsal.Id, new[]
        {
            new AttendanceBatchItem { MemberId = member.Id, Attended = true, MarkAsUnpaid = false }
        }, TestContext.Current.CancellationToken);

        // Fee created with PaidAtCreation=true
        var fee = await _db.Fees.FirstOrDefaultAsync(f => f.MemberId == member.Id, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(fee);
        Assert.True(fee!.PaidAtCreation);
        Assert.Equal(FeeType.Attendance, fee.FeeType);
        Assert.Equal(10m, fee.Amount);
        Assert.Equal(rehearsal.Id, fee.RehearsalId);

        // GL pairs: accrual + payment = 4 transactions
        var transactions = await _db.Transactions.Where(t => t.MemberId == member.Id).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(4, transactions.Count);

        // Accrual debit: MemberReceivable
        Assert.Contains(transactions, t => t.DebitAmount == 10m && t.AccountId == MemberReceivableAccountId);
        // Accrual credit: Income account
        Assert.Contains(transactions, t => t.CreditAmount == 10m && t.GLAccount == "1000");
        // Payment debit: Cash
        Assert.Contains(transactions, t => t.DebitAmount == 10m && t.AccountId == CashAccountId);
        // Payment credit: MemberReceivable
        Assert.Contains(transactions, t => t.CreditAmount == 10m && t.AccountId == MemberReceivableAccountId);

        // Payment record auto-created
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.MemberId == member.Id, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(payment);
        Assert.Equal(PaymentMethod.Cash, payment!.PaymentMethod);
        Assert.Equal(PaymentType.Attendance, payment.PaymentType);
        Assert.Equal(10m, payment.Amount);
    }

    // --- Mark as unpaid ---

    [Fact]
    public async Task RecordBatch_MarkAsUnpaid_CreatesFeeWithPaidAtCreationFalse_AndOnlyAccrualGLPair()
    {
        var member = await AddActiveMember("Alice");
        var rehearsal = await ScheduleRehearsal();

        var attendanceSvc = BuildAttendanceService();
        await attendanceSvc.RecordBatchAsync(rehearsal.Id, new[]
        {
            new AttendanceBatchItem { MemberId = member.Id, Attended = true, MarkAsUnpaid = true }
        }, TestContext.Current.CancellationToken);

        var fee = await _db.Fees.FirstOrDefaultAsync(f => f.MemberId == member.Id, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(fee);
        Assert.False(fee!.PaidAtCreation);

        // Only 2 GL transactions (accrual pair only, no payment pair)
        var transactions = await _db.Transactions.Where(t => t.MemberId == member.Id).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, transactions.Count);

        // No Payment record
        Assert.Equal(0, await _db.Payments.CountAsync(p => p.MemberId == member.Id, cancellationToken: TestContext.Current.CancellationToken));
    }

    // --- Inactive member ---

    [Fact]
    public async Task RecordBatch_InactiveMember_CreatesAttendanceRecord_ButNoFee()
    {
        var member = await AddInactiveMember("Inactive Bob");
        var rehearsal = await ScheduleRehearsal();

        var attendanceSvc = BuildAttendanceService();
        await attendanceSvc.RecordBatchAsync(rehearsal.Id, new[]
        {
            new AttendanceBatchItem { MemberId = member.Id, Attended = true, MarkAsUnpaid = false }
        }, TestContext.Current.CancellationToken);

        // No fee, no GL, no payment
        Assert.Equal(0, await _db.Fees.CountAsync(f => f.MemberId == member.Id, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, await _db.Transactions.CountAsync(t => t.MemberId == member.Id, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, await _db.Payments.CountAsync(p => p.MemberId == member.Id, cancellationToken: TestContext.Current.CancellationToken));
    }

    // --- StoredAttendanceRate frozen ---

    [Fact]
    public async Task RecordBatch_FreezesStoredAttendanceRate_OnRehearsal()
    {
        var member1 = await AddActiveMember("Carol");
        var member2 = await AddActiveMember("Dave");
        var rehearsal = await ScheduleRehearsal();

        var attendanceSvc = BuildAttendanceService();
        await attendanceSvc.RecordBatchAsync(rehearsal.Id, new[]
        {
            new AttendanceBatchItem { MemberId = member1.Id, Attended = true },
            new AttendanceBatchItem { MemberId = member2.Id, Attended = false }
        }, TestContext.Current.CancellationToken);

        var updated = await new RehearsalRepository(_db).GetByIdAsync(rehearsal.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated!.StoredAttendanceRate);
        // 1 present out of 2 active = 50%
        Assert.Equal(50m, updated.StoredAttendanceRate!.Value);
    }

    // --- Idempotency ---

    [Fact]
    public async Task RecordBatch_CalledTwice_IsIdempotent_NoDoubleCharging()
    {
        var member = await AddActiveMember("Eve");
        var rehearsal = await ScheduleRehearsal();
        var attendanceSvc = BuildAttendanceService();

        var items = new[] { new AttendanceBatchItem { MemberId = member.Id, Attended = true } };

        await attendanceSvc.RecordBatchAsync(rehearsal.Id, items, TestContext.Current.CancellationToken);
        await attendanceSvc.RecordBatchAsync(rehearsal.Id, items, TestContext.Current.CancellationToken); // second call is idempotent

        // Only one fee created
        Assert.Equal(1, await _db.Fees.CountAsync(f => f.MemberId == member.Id, cancellationToken: TestContext.Current.CancellationToken));
    }

    // --- No edit UI after save ---

    [Fact]
    public async Task AfterAttendanceRecorded_StoredAttendanceRate_IsImmutable()
    {
        var member = await AddActiveMember("Frank");
        var rehearsal = await ScheduleRehearsal();
        var attendanceSvc = BuildAttendanceService();

        await attendanceSvc.RecordBatchAsync(rehearsal.Id, new[]
        {
            new AttendanceBatchItem { MemberId = member.Id, Attended = true }
        }, TestContext.Current.CancellationToken);

        // Calling FreezeAttendanceRateAsync again with a different count must be a no-op
        var rehearsalSvc = BuildRehearsalService();
        var before = (await new RehearsalRepository(_db).GetByIdAsync(rehearsal.Id, TestContext.Current.CancellationToken))!.StoredAttendanceRate;

        await rehearsalSvc.FreezeAttendanceRateAsync(rehearsal.Id, rehearsal.Date, presentCount: 999, ct: TestContext.Current.CancellationToken);

        var after = (await new RehearsalRepository(_db).GetByIdAsync(rehearsal.Id, TestContext.Current.CancellationToken))!.StoredAttendanceRate;
        Assert.Equal(before, after);
    }

    // --- AttendanceRollService (issue #257) ---

    [Fact]
    public async Task GenerateAsync_Throws_EntityNotFoundException_ForUnknownRehearsalId()
    {
        var svc = BuildAttendanceRollService();

        await Assert.ThrowsAsync<StageFright.Core.Exceptions.EntityNotFoundException>(
            () => svc.GenerateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GenerateAsync_Returns_OnlyActiveMembers_SortedBySurnameThenFirstName_ExcludingSoftDeleted()
    {
        var smithBob = await AddActiveMember("Bob");
        smithBob.LastName = "Smith";
        var smithAlice = await AddActiveMember("Alice");
        smithAlice.LastName = "Smith";
        var jones = await AddActiveMember("Zoe");
        jones.LastName = "Jones";
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var deletedMember = await AddActiveMember("Deleted");
        deletedMember.LastName = "Ghost";
        deletedMember.IsDeleted = true;
        deletedMember.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var rehearsal = await ScheduleRehearsal();
        var svc = BuildAttendanceRollService();

        var result = await svc.GenerateAsync(rehearsal.Id, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Members.Count);
        Assert.Equal("Jones", result.Members[0].LastName);
        Assert.Equal("Smith", result.Members[1].LastName);
        Assert.Equal("Alice", result.Members[1].FirstName);
        Assert.Equal("Smith", result.Members[2].LastName);
        Assert.Equal("Bob", result.Members[2].FirstName);
        Assert.DoesNotContain(result.Members, m => m.LastName == "Ghost");
    }

    [Fact]
    public async Task GenerateAsync_Returns_EmptyList_WhenNoActiveMembers()
    {
        var rehearsal = await ScheduleRehearsal();
        var svc = BuildAttendanceRollService();

        var result = await svc.GenerateAsync(rehearsal.Id, TestContext.Current.CancellationToken);

        Assert.Empty(result.Members);
    }

    [Fact]
    public async Task GenerateAsync_ReflectsRealAttendanceAndFeePaymentState_AfterAttendanceRecorded()
    {
        var paidMember = await AddActiveMember("Paid");
        var unpaidMember = await AddActiveMember("Unpaid");
        var absentMember = await AddActiveMember("Absent");

        var rehearsal = await ScheduleRehearsal();

        var attendanceSvc = BuildAttendanceService();
        await attendanceSvc.RecordBatchAsync(rehearsal.Id, new[]
        {
            new AttendanceBatchItem { MemberId = paidMember.Id, Attended = true, MarkAsUnpaid = false },
            new AttendanceBatchItem { MemberId = unpaidMember.Id, Attended = true, MarkAsUnpaid = true },
            new AttendanceBatchItem { MemberId = absentMember.Id, Attended = false }
        }, TestContext.Current.CancellationToken);

        var svc = BuildAttendanceRollService();
        var result = await svc.GenerateAsync(rehearsal.Id, TestContext.Current.CancellationToken);

        var paidRow = result.Members.Single(m => m.FirstName == "Paid");
        Assert.True(paidRow.Attended);
        Assert.True(paidRow.RehearsalFeePaid);

        var unpaidRow = result.Members.Single(m => m.FirstName == "Unpaid");
        Assert.True(unpaidRow.Attended);
        Assert.False(unpaidRow.RehearsalFeePaid);

        var absentRow = result.Members.Single(m => m.FirstName == "Absent");
        Assert.False(absentRow.Attended);
        Assert.False(absentRow.RehearsalFeePaid);
    }

    [Fact]
    public async Task GenerateAsync_MembershipIsPointInTime_NotBasedOnTodaysStatus()
    {
        var rehearsal = await ScheduleRehearsal(); // 2026-06-15

        // Active as of the rehearsal date but inactivated afterward -> still on the roll
        var laterInactivated = await AddActiveMember("StillOnRoll");
        laterInactivated.Status = MemberStatus.Inactive;
        laterInactivated.InactivateDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Not active until after the rehearsal date -> excluded from the roll
        var joinedLater = await AddActiveMember("NotYetJoined");
        joinedLater.ActivateDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = BuildAttendanceRollService();
        var result = await svc.GenerateAsync(rehearsal.Id, TestContext.Current.CancellationToken);

        Assert.Contains(result.Members, m => m.FirstName == "StillOnRoll");
        Assert.DoesNotContain(result.Members, m => m.FirstName == "NotYetJoined");
    }

    // --- Helpers ---

    private AttendanceRollService BuildAttendanceRollService()
    {
        var rehearsalRepo = new RehearsalRepository(_db);
        var memberRepo = new MemberRepository(_db);
        var attendanceRepo = new AttendanceRepository(_db);
        var memberBalanceSvc = BuildMemberBalanceService();
        var feeRepo = new FeeRepository(_db);
        return new AttendanceRollService(rehearsalRepo, memberRepo, attendanceRepo, memberBalanceSvc, feeRepo);
    }

    private MemberBalanceService BuildMemberBalanceService()
    {
        var memberRepo = new MemberRepository(_db);
        var feeRepo = new FeeRepository(_db);
        var glRepo = new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db)));
        return new MemberBalanceService(memberRepo, feeRepo, glRepo);
    }

    private FeeService BuildFeeService()
    {
        var memberRepo = new MemberRepository(_db);
        var feeRepo = new FeeRepository(_db);
        var glRepo = new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db)));
        var accountRepo = new AccountRepository(_db);
        var settingsRepo = new SettingsRepository(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditSvc = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var unitOfWork = new UnitOfWork(_db);
        return new FeeService(memberRepo, feeRepo, glRepo, accountRepo, settingsRepo, auditSvc, unitOfWork, RealLocalizer.Instance);
    }

    private PaymentService BuildPaymentService()
    {
        var feeRepo = new FeeRepository(_db);
        var paymentRepo = new PaymentRepository(_db, BuildAuditService());
        var glRepo = new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db)));
        var memberRepo = new MemberRepository(_db);
        var unitOfWork = new UnitOfWork(_db);
        return new PaymentService(feeRepo, paymentRepo, glRepo, memberRepo, BuildAuditService(), unitOfWork, RealLocalizer.Instance);
    }

    private AuditTrailService BuildAuditService()
    {
        var auditRepo = new AuditTrailRepository(_db);
        return new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
    }

    private MemberService BuildMemberService()
    {
        var memberRepo = new MemberRepository(_db);
        var committeeRepo = new CommitteePositionRecordRepository(_db);
        var settingsRepo = new SettingsRepository(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditSvc = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var ageCalc = new AgeCalculationService(RealLocalizer.Instance);
        var validation = new MemberValidationService(
            ageCalc, new StubStringLocalizer<StageFright.Core.Modules.Localization.Resources.ValidationResource>());
        var unitOfWork = new UnitOfWork(_db);
        return new MemberService(memberRepo, committeeRepo, validation, settingsRepo, auditSvc, unitOfWork);
    }

    private RehearsalService BuildRehearsalService()
    {
        var rehearsalRepo = new RehearsalRepository(_db);
        var memberRepo = new MemberRepository(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditSvc = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var unitOfWork = new UnitOfWork(_db);
        return new RehearsalService(rehearsalRepo, memberRepo, auditSvc, unitOfWork);
    }

    private AttendanceService BuildAttendanceService()
    {
        var rehearsalRepo = new RehearsalRepository(_db);
        var attendanceRepo = new AttendanceRepository(_db);
        var memberRepo = new MemberRepository(_db);
        var feeRepo = new FeeRepository(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditSvc = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var paymentRepo = new PaymentRepository(_db, auditSvc);
        var glRepo = new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db)));
        var accountRepo = new AccountRepository(_db);
        var settingsRepo = new SettingsRepository(_db);
        var unitOfWork = new UnitOfWork(_db);
        var rehearsalSvc = BuildRehearsalService();
        return new AttendanceService(rehearsalRepo, attendanceRepo, memberRepo, feeRepo, paymentRepo,
            glRepo, accountRepo, settingsRepo, auditSvc, unitOfWork, rehearsalSvc, RealLocalizer.Instance);
    }

    private async Task<Member> AddActiveMember(string name)
    {
        var member = new Member
        {
            Id = Guid.NewGuid(), FirstName = name, StreetAddress = "1 Test St",
            Status = MemberStatus.Active, ActivateDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync();
        return member;
    }

    private async Task<Member> AddInactiveMember(string name)
    {
        var member = new Member
        {
            Id = Guid.NewGuid(), FirstName = name, StreetAddress = "2 Test St",
            Status = MemberStatus.Inactive,
            InactivateDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync();
        return member;
    }

    private async Task<Rehearsal> ScheduleRehearsal()
    {
        var svc = BuildRehearsalService();
        return await svc.ScheduleAsync(new ScheduleRehearsalRequest
        {
            Date = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            Time = TimeSpan.FromHours(19)
        });
    }
}
