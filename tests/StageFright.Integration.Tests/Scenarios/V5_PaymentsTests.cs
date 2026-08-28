using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V5: payment recording against a real in-memory SQLite database.
/// Verifies FIFO GL allocation, partial payments, overpayments, Notes-only edits, and immutability.
/// Uses fee + GL rows seeded via FeeService so the full stack is exercised.
/// </summary>
public sealed class V5_PaymentsTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid IncomeAccountId = Guid.NewGuid();
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

        _db.Accounts.Add(new Account
        {
            Id = IncomeAccountId, Name = "Membership Income",
            Type = AccountType.Income, AccountNumber = "1000",
            SortOrder = 0, IsSystem = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });

        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Test Choir",
            AnnualFee = 50m, AttendanceFee = 10m,
            MembershipRenewalMonth = 6, CommitteeRenewalMonth = 1,
            MaxAgeRangeYears = 150, MinimumMemberAge = 0,
            Theme = Theme.Light, SchemaVersion = "1.0.0",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- FIFO full payment ---

    [Fact]
    public async Task FIFO_FullPayment_ClearsBothFees_AndReducesBalanceToZero()
    {
        var member = await AddActiveMemberAsync("Alice");

        // Apply two annual fees in different years so they're both outstanding
        await ApplyFeeForMemberAsync(member.Id, 2025, 50m);
        await ApplyFeeForMemberAsync(member.Id, 2026, 60m);

        var glRepo = new GLRepository(_db);
        var balanceBefore = await glRepo.GetMemberBalanceAsync(member.Id, TestContext.Current.CancellationToken);
        Assert.Equal(110m, balanceBefore);

        var paymentSvc = BuildPaymentService();
        var payment = await paymentSvc.RecordAsync(new RecordPaymentRequest
        {
            MemberId = member.Id,
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 110m,
            PaymentMethod = PaymentMethod.Cash,
            PaymentType = PaymentType.Annual
        }, TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, payment.Id);

        var balanceAfter = await glRepo.GetMemberBalanceAsync(member.Id, TestContext.Current.CancellationToken);
        Assert.Equal(0m, balanceAfter);
    }

    // --- Partial payment ---

    [Fact]
    public async Task PartialPayment_AllocatesFromOldestFeeFirst()
    {
        var member = await AddActiveMemberAsync("Bob");

        await ApplyFeeForMemberAsync(member.Id, 2025, 50m);
        await ApplyFeeForMemberAsync(member.Id, 2026, 60m);

        var paymentSvc = BuildPaymentService();
        await paymentSvc.RecordAsync(new RecordPaymentRequest
        {
            MemberId = member.Id,
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 50m,
            PaymentMethod = PaymentMethod.Cash,
            PaymentType = PaymentType.Annual
        }, TestContext.Current.CancellationToken);

        var glRepo = new GLRepository(_db);
        var balanceAfter = await glRepo.GetMemberBalanceAsync(member.Id, TestContext.Current.CancellationToken);

        // $50 payment covers 2025 fee; 2026 fee ($60) remains
        Assert.Equal(60m, balanceAfter);
    }

    // --- Overpayment ---

    [Fact]
    public async Task Overpayment_CreatesNegativeBalance_ForMember()
    {
        var member = await AddActiveMemberAsync("Carol");

        await ApplyFeeForMemberAsync(member.Id, 2026, 50m);

        var paymentSvc = BuildPaymentService();
        await paymentSvc.RecordAsync(new RecordPaymentRequest
        {
            MemberId = member.Id,
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 70m,
            PaymentMethod = PaymentMethod.Cash,
            PaymentType = PaymentType.Annual
        }, TestContext.Current.CancellationToken);

        var glRepo = new GLRepository(_db);
        var balance = await glRepo.GetMemberBalanceAsync(member.Id, TestContext.Current.CancellationToken);

        // $70 paid, $50 owed → $20 credit = -$20 outstanding
        Assert.Equal(-20m, balance);
    }

    // --- Notes-only edit ---

    [Fact]
    public async Task UpdateNotesAsync_UpdatesNotesOnExistingPayment()
    {
        var member = await AddActiveMemberAsync("Diana");
        await ApplyFeeForMemberAsync(member.Id, 2026, 50m);

        var paymentSvc = BuildPaymentService();
        var payment = await paymentSvc.RecordAsync(new RecordPaymentRequest
        {
            MemberId = member.Id,
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 50m,
            PaymentMethod = PaymentMethod.Cash,
            PaymentType = PaymentType.Annual,
            Notes = "original note"
        }, TestContext.Current.CancellationToken);

        await paymentSvc.UpdateNotesAsync(payment.Id, "updated note", TestContext.Current.CancellationToken);

        var fromDb = await _db.Payments.FindAsync(new object?[] { payment.Id }, TestContext.Current.CancellationToken);
        Assert.Equal("updated note", fromDb!.Notes);
    }

    // --- Immutability ---

    [Fact]
    public async Task UpdateNotesAsync_ForUnknownPaymentId_ThrowsEntityNotFoundException()
    {
        var paymentSvc = BuildPaymentService();

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => paymentSvc.UpdateNotesAsync(Guid.NewGuid(), "anything", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PaymentRepository_HasNoUpdateAsync_EnforcingImmutability()
    {
        var repoType = typeof(PaymentRepository);
        var updateAsyncMethod = repoType.GetMethod("UpdateAsync");
        Assert.Null(updateAsyncMethod);
    }

    // --- Sequential payments (FeeId re-allocation regression) ---

    [Fact]
    public async Task SecondPayment_DoesNotReallocateToFeeAlreadySettledByFirstPayment()
    {
        var member = await AddActiveMemberAsync("Frank");

        var fee2025Id = await ApplyFeeForMemberAsync(member.Id, 2025, 50m);
        var fee2026Id = await ApplyFeeForMemberAsync(member.Id, 2026, 60m);

        var paymentSvc = BuildPaymentService();

        // First payment exactly clears the older (2025) fee.
        await paymentSvc.RecordAsync(new RecordPaymentRequest
        {
            MemberId = member.Id,
            Date = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 50m,
            PaymentMethod = PaymentMethod.Cash,
            PaymentType = PaymentType.Annual
        }, TestContext.Current.CancellationToken);

        // Second payment should clear the 2026 fee — not re-touch the already-settled 2025 fee.
        await paymentSvc.RecordAsync(new RecordPaymentRequest
        {
            MemberId = member.Id,
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 60m,
            PaymentMethod = PaymentMethod.Cash,
            PaymentType = PaymentType.Annual
        }, TestContext.Current.CancellationToken);

        var glRepo = new GLRepository(_db);
        var balance = await glRepo.GetMemberBalanceAsync(member.Id, TestContext.Current.CancellationToken);
        Assert.Equal(0m, balance);

        var fee2025Credits = await _db.Transactions
            .Where(t => t.FeeId == fee2025Id && t.AccountId == MemberReceivableAccountId)
            .SumAsync(t => t.CreditAmount, cancellationToken: TestContext.Current.CancellationToken);
        var fee2026Credits = await _db.Transactions
            .Where(t => t.FeeId == fee2026Id && t.AccountId == MemberReceivableAccountId)
            .SumAsync(t => t.CreditAmount, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(50m, fee2025Credits);
        Assert.Equal(60m, fee2026Credits);
    }

    // --- GL pair linkage ---

    [Fact]
    public async Task RecordPayment_GLPairs_AllLinkedToPaymentId()
    {
        var member = await AddActiveMemberAsync("Eve");
        await ApplyFeeForMemberAsync(member.Id, 2026, 50m);

        var paymentSvc = BuildPaymentService();
        var payment = await paymentSvc.RecordAsync(new RecordPaymentRequest
        {
            MemberId = member.Id,
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 50m,
            PaymentMethod = PaymentMethod.Cash,
            PaymentType = PaymentType.Annual
        }, TestContext.Current.CancellationToken);

        var paymentTxns = await _db.Transactions
            .Where(t => t.PaymentId == payment.Id)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, paymentTxns.Count);
        Assert.All(paymentTxns, t => Assert.Equal(payment.Id, t.PaymentId));
    }

    // --- SelectedFeeIds allocation ---

    [Fact]
    public async Task SelectedFeeIds_FullAllocation_ClearsOnlyCheckedFees()
    {
        var member = await AddActiveMemberAsync("Grace");

        var fee2025Id = await ApplyFeeForMemberAsync(member.Id, 2025, 50m);
        var fee2026Id = await ApplyFeeForMemberAsync(member.Id, 2026, 60m);

        var paymentSvc = BuildPaymentService();
        var payment = await paymentSvc.RecordAsync(new RecordPaymentRequest
        {
            MemberId = member.Id,
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 110m,
            PaymentMethod = PaymentMethod.Cash,
            PaymentType = PaymentType.Annual,
            SelectedFeeIds = [fee2025Id, fee2026Id]
        }, TestContext.Current.CancellationToken);

        var glRepo = new GLRepository(_db);
        var balanceAfter = await glRepo.GetMemberBalanceAsync(member.Id, TestContext.Current.CancellationToken);
        Assert.Equal(0m, balanceAfter);

        var paymentTxns = await _db.Transactions
            .Where(t => t.PaymentId == payment.Id && t.FeeId != null)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.All(paymentTxns, t => Assert.True(t.FeeId == fee2025Id || t.FeeId == fee2026Id));
    }

    [Fact]
    public async Task SelectedFeeIds_PartialAllocation_SettlesOldestCheckedFeeFirst_LeavesUncheckedFeeUntouched()
    {
        var member = await AddActiveMemberAsync("Henry");

        var fee2025Id = await ApplyFeeForMemberAsync(member.Id, 2025, 50m);
        var fee2026Id = await ApplyFeeForMemberAsync(member.Id, 2026, 60m);

        var paymentSvc = BuildPaymentService();
        await paymentSvc.RecordAsync(new RecordPaymentRequest
        {
            MemberId = member.Id,
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 30m,
            PaymentMethod = PaymentMethod.Cash,
            PaymentType = PaymentType.Annual,
            SelectedFeeIds = [fee2025Id]
        }, TestContext.Current.CancellationToken);

        var fee2025Credits = await _db.Transactions
            .Where(t => t.FeeId == fee2025Id && t.AccountId == MemberReceivableAccountId)
            .SumAsync(t => t.CreditAmount, cancellationToken: TestContext.Current.CancellationToken);
        var fee2026Credits = await _db.Transactions
            .Where(t => t.FeeId == fee2026Id && t.AccountId == MemberReceivableAccountId)
            .SumAsync(t => t.CreditAmount, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(30m, fee2025Credits);
        Assert.Equal(0m, fee2026Credits);
    }

    [Fact]
    public async Task SelectedFeeIds_EmptySelection_ThrowsValidationException()
    {
        var member = await AddActiveMemberAsync("Iris");
        await ApplyFeeForMemberAsync(member.Id, 2026, 50m);

        var paymentSvc = BuildPaymentService();

        await Assert.ThrowsAsync<ValidationException>(() => paymentSvc.RecordAsync(new RecordPaymentRequest
        {
            MemberId = member.Id,
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 50m,
            PaymentMethod = PaymentMethod.Cash,
            PaymentType = PaymentType.Annual,
            SelectedFeeIds = []
        }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SelectedFeeIds_AmountExceedsSelectedTotal_ThrowsValidationException()
    {
        var member = await AddActiveMemberAsync("Jack");
        var fee2025Id = await ApplyFeeForMemberAsync(member.Id, 2025, 50m);
        await ApplyFeeForMemberAsync(member.Id, 2026, 60m);

        var paymentSvc = BuildPaymentService();

        await Assert.ThrowsAsync<ValidationException>(() => paymentSvc.RecordAsync(new RecordPaymentRequest
        {
            MemberId = member.Id,
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 70m, // exceeds the single selected fee's $50 remaining
            PaymentMethod = PaymentMethod.Cash,
            PaymentType = PaymentType.Annual,
            SelectedFeeIds = [fee2025Id]
        }, TestContext.Current.CancellationToken));
    }

    // --- Helpers ---

    private PaymentService BuildPaymentService()
    {
        var feeRepo = new FeeRepository(_db);
        var paymentRepo = new PaymentRepository(_db, BuildAuditService());
        var glRepo = new GLRepository(_db);
        var memberRepo = new MemberRepository(_db);
        var audit = BuildAuditService();
        var unitOfWork = new UnitOfWork(_db);
        return new PaymentService(feeRepo, paymentRepo, glRepo, memberRepo, audit, unitOfWork, RealLocalizer.Instance);
    }

    private static AuditTrailService BuildAuditService()
    {
        var auditRepo = NSubstitute.Substitute.For<StageFright.Core.Contracts.IAuditTrailRepository>();
        return new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
    }

    private async Task<Guid> ApplyFeeForMemberAsync(Guid memberId, int year, decimal amount)
    {
        // Directly seed Fee + GL debit (avoids FeeService complexity)
        var feeId = Guid.NewGuid();
        var feeDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = DateTime.UtcNow;

        _db.Fees.Add(new Fee
        {
            Id = feeId, MemberId = memberId, FeeType = FeeType.Annual,
            Amount = amount, FeeDate = feeDate,
            DueDate = new DateTime(year, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            PaidAtCreation = false, CreatedAt = now
        });

        // GL debit MemberReceivable / credit Income
        _db.Transactions.AddRange(
            new Transaction
            {
                Id = Guid.NewGuid(), Date = feeDate, AccountId = MemberReceivableAccountId,
                DebitAmount = amount, CreditAmount = 0m, GLAccount = "0101",
                MemberId = memberId, FeeId = feeId, CreatedAt = now
            },
            new Transaction
            {
                Id = Guid.NewGuid(), Date = feeDate, AccountId = IncomeAccountId,
                DebitAmount = 0m, CreditAmount = amount, GLAccount = "1000",
                MemberId = null, FeeId = feeId, CreatedAt = now
            });

        await _db.SaveChangesAsync();
        return feeId;
    }

    private async Task<Member> AddActiveMemberAsync(string name)
    {
        var member = new Member
        {
            Id = Guid.NewGuid(), FirstName = name, StreetAddress = "1 Test St",
            Status = MemberStatus.Active,
            ActivateDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            JoinDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync();
        return member;
    }
}
