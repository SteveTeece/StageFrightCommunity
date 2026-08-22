using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V12: reactivation forgiveness on outstanding fees.
/// Verifies prior-year default selection, current-year override, GL write-offs,
/// balance reduction, and Fee record immutability.
/// Uses a real SQLite in-memory database with full EF migrations.
/// </summary>
public sealed class V12_ReactivationForgivenessTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid MemberReceivableAccountId = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid BadDebtAccountId = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid IncomeAccountId = Guid.NewGuid();

    private static readonly int CurrentYear = DateTime.UtcNow.Year;

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

        await _db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- GetForgivenessItemsAsync ---

    [Fact]
    public async Task GetForgivenessItems_PriorYearFee_IsDefaultForgiven()
    {
        var member = await AddActiveMemberAsync("Alice");
        await SeedFeeAsync(member.Id, CurrentYear - 1, 50m);

        var svc = BuildForgivenessService();
        var items = await svc.GetForgivenessItemsAsync(member.Id, TestContext.Current.CancellationToken);

        var item = Assert.Single(items);
        Assert.True(item.IsDefaultForgiven);
    }

    [Fact]
    public async Task GetForgivenessItems_CurrentYearFee_IsNotDefaultForgiven()
    {
        var member = await AddActiveMemberAsync("Bob");
        await SeedFeeAsync(member.Id, CurrentYear, 50m);

        var svc = BuildForgivenessService();
        var items = await svc.GetForgivenessItemsAsync(member.Id, TestContext.Current.CancellationToken);

        var item = Assert.Single(items);
        Assert.False(item.IsDefaultForgiven);
    }

    // --- ApplyForgivenessAsync ---

    [Fact]
    public async Task ApplyForgiveness_CreatesDebitBadDebt_CreditMemberReceivable_PerFee()
    {
        var member = await AddActiveMemberAsync("Carol");
        var fee2024Id = await SeedFeeAsync(member.Id, CurrentYear - 1, 50m);

        await SeedGLDebitAsync(member.Id, fee2024Id, 50m);

        var balanceBefore = await GetBalanceAsync(member.Id);
        Assert.Equal(50m, balanceBefore);

        var svc = BuildForgivenessService();
        await svc.ApplyForgivenessAsync(member.Id, new[] { fee2024Id }, TestContext.Current.CancellationToken);

        var balanceAfter = await GetBalanceAsync(member.Id);
        Assert.Equal(0m, balanceAfter);

        // GL pair created: Debit BadDebt / Credit MemberReceivable
        var forgivenessTxns = await _db.Transactions
            .Where(t => t.FeeId == fee2024Id && t.AccountId == BadDebtAccountId)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(forgivenessTxns);
        Assert.Equal(50m, forgivenessTxns[0].DebitAmount);
    }

    [Fact]
    public async Task ApplyForgiveness_CurrentYearFeeOverride_CanAlsoBeWrittenOff()
    {
        var member = await AddActiveMemberAsync("Diana");
        var currentFeeId = await SeedFeeAsync(member.Id, CurrentYear, 50m);

        await SeedGLDebitAsync(member.Id, currentFeeId, 50m);

        var svc = BuildForgivenessService();
        // Override: explicitly include current-year fee
        await svc.ApplyForgivenessAsync(member.Id, new[] { currentFeeId }, TestContext.Current.CancellationToken);

        var balance = await GetBalanceAsync(member.Id);
        Assert.Equal(0m, balance);
    }

    [Fact]
    public async Task ApplyForgiveness_MultipleFees_CreatesOnePairPerSelectedFee()
    {
        var member = await AddActiveMemberAsync("Eve");
        var fee2024Id = await SeedFeeAsync(member.Id, CurrentYear - 2, 50m);
        var fee2025Id = await SeedFeeAsync(member.Id, CurrentYear - 1, 60m);

        await SeedGLDebitAsync(member.Id, fee2024Id, 50m);
        await SeedGLDebitAsync(member.Id, fee2025Id, 60m);

        var svc = BuildForgivenessService();
        await svc.ApplyForgivenessAsync(member.Id, new[] { fee2024Id, fee2025Id }, TestContext.Current.CancellationToken);

        var badDebtTxns = await _db.Transactions
            .Where(t => t.AccountId == BadDebtAccountId && t.MemberId == member.Id)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, badDebtTxns.Count);
    }

    // --- Fee record immutability ---

    [Fact]
    public async Task ApplyForgiveness_FeeRecords_AreNotModified()
    {
        var member = await AddActiveMemberAsync("Frank");
        var feeId = await SeedFeeAsync(member.Id, CurrentYear - 1, 50m);
        await SeedGLDebitAsync(member.Id, feeId, 50m);

        var feeBefore = await _db.Fees.FindAsync(new object?[] { feeId }, TestContext.Current.CancellationToken);
        var amountBefore = feeBefore!.Amount;
        var createdAtBefore = feeBefore.CreatedAt;

        var svc = BuildForgivenessService();
        await svc.ApplyForgivenessAsync(member.Id, new[] { feeId }, TestContext.Current.CancellationToken);

        // Re-fetch from DB
        _db.ChangeTracker.Clear();
        var feeAfter = await _db.Fees.FindAsync(new object?[] { feeId }, TestContext.Current.CancellationToken);

        Assert.Equal(amountBefore, feeAfter!.Amount);
        Assert.Equal(createdAtBefore, feeAfter.CreatedAt);
        // Fee count unchanged (no new Fee rows added)
        Assert.Equal(1, await _db.Fees.Where(f => f.MemberId == member.Id).CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    // --- Helpers ---

    private ReactivationForgivenessService BuildForgivenessService()
    {
        var feeRepo = new FeeRepository(_db);
        var glRepo = new GLRepository(_db);
        var memberRepo = new MemberRepository(_db);
        var settingsRepo = new SettingsRepository(_db);
        var auditRepo = NSubstitute.Substitute.For<StageFright.Core.Contracts.IAuditTrailRepository>();
        var audit = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var unitOfWork = new UnitOfWork(_db);
        return new ReactivationForgivenessService(feeRepo, glRepo, memberRepo, settingsRepo, audit, unitOfWork);
    }

    private async Task<decimal> GetBalanceAsync(Guid memberId)
    {
        var glRepo = new GLRepository(_db);
        return await glRepo.GetMemberBalanceAsync(memberId);
    }

    private async Task<Guid> SeedFeeAsync(Guid memberId, int year, decimal amount)
    {
        var feeId = Guid.NewGuid();
        _db.Fees.Add(new Fee
        {
            Id = feeId, MemberId = memberId, FeeType = FeeType.Annual,
            Amount = amount,
            FeeDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(year, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            PaidAtCreation = false, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return feeId;
    }

    private async Task SeedGLDebitAsync(Guid memberId, Guid feeId, decimal amount)
    {
        var date = DateTime.UtcNow;
        _db.Transactions.AddRange(
            new Transaction
            {
                Id = Guid.NewGuid(), Date = date, AccountId = MemberReceivableAccountId,
                DebitAmount = amount, CreditAmount = 0m, GLAccount = "0101",
                MemberId = memberId, FeeId = feeId, CreatedAt = date
            },
            new Transaction
            {
                Id = Guid.NewGuid(), Date = date, AccountId = IncomeAccountId,
                DebitAmount = 0m, CreditAmount = amount, GLAccount = "1000",
                MemberId = null, FeeId = feeId, CreatedAt = date
            });
        await _db.SaveChangesAsync();
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
