using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for GLRepository against a real SQLite in-memory database.
/// Verifies balance queries, date-range filtering, and balance totals for Trial Balance.
/// </summary>
public sealed class GLRepositoryIntegrationTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;
    private GLRepository _sut = null!;

    // System account GUIDs (seeded by StageFrightDbContext migration)
    private static readonly Guid CashAccountId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid MemberReceivableAccountId = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid BadDebtAccountId = new("00000000-0000-0000-0000-000000000003");

    // Test-only income account seeded in InitializeAsync
    private static readonly Guid IncomeAccountId = new("00000000-0000-0000-0000-000000000010");

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        // Seed a test income account for GL transactions
        _db.Accounts.Add(new Account
        {
            Id = IncomeAccountId, Name = "Test Income", Type = AccountType.Income,
            AccountNumber = "1000", SortOrder = 10, IsSystem = false,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await _db.SaveChangesAsync();

        _sut = new GLRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- GetMemberBalanceAsync ---

    [Fact]
    public async Task GetMemberBalance_ReturnsZero_WhenNoTransactions()
    {
        var memberId = Guid.NewGuid();

        var balance = await _sut.GetMemberBalanceAsync(memberId);

        Assert.Equal(0m, balance);
    }

    [Fact]
    public async Task GetMemberBalance_ReturnsDebitMinusCredit_ForMemberReceivable()
    {
        var memberId = await SeedMemberAsync();

        // Fee accrual: Debit MemberReceivable $50 / Credit Income $50
        await AddGLPairAsync(
            debitAccount: "0101", debitAccountId: MemberReceivableAccountId, debitAmount: 50m, debitMemberId: memberId,
            creditAccount: "1000", creditAccountId: IncomeAccountId, creditAmount: 50m, creditMemberId: null);

        var balance = await _sut.GetMemberBalanceAsync(memberId);

        Assert.Equal(50m, balance);
    }

    [Fact]
    public async Task GetMemberBalance_ReducedByPaymentCredit()
    {
        var memberId = await SeedMemberAsync();

        // Fee accrual
        await AddGLPairAsync("0101", MemberReceivableAccountId, 50m, memberId,
                              "1000", IncomeAccountId, 50m, null);

        // Payment pair: Debit Cash / Credit MemberReceivable
        await AddGLPairAsync("0100", CashAccountId, 30m, memberId,
                              "0101", MemberReceivableAccountId, 30m, memberId);

        var balance = await _sut.GetMemberBalanceAsync(memberId);

        Assert.Equal(20m, balance);
    }

    [Fact]
    public async Task GetMemberBalance_IsolatedToMember_OtherMembersNotIncluded()
    {
        var member1 = await SeedMemberAsync();
        var member2 = await SeedMemberAsync();

        await AddGLPairAsync("0101", MemberReceivableAccountId, 100m, member1,
                              "1000", IncomeAccountId, 100m, null);

        await AddGLPairAsync("0101", MemberReceivableAccountId, 75m, member2,
                              "1000", IncomeAccountId, 75m, null);

        var balance1 = await _sut.GetMemberBalanceAsync(member1);
        var balance2 = await _sut.GetMemberBalanceAsync(member2);

        Assert.Equal(100m, balance1);
        Assert.Equal(75m, balance2);
    }

    // --- GetTotalOutstandingAsync ---

    [Fact]
    public async Task GetTotalOutstanding_ReturnsZero_WhenNoTransactions()
    {
        var total = await _sut.GetTotalOutstandingAsync();

        Assert.Equal(0m, total);
    }

    [Fact]
    public async Task GetTotalOutstanding_SumsAllMemberReceivableEntries()
    {
        var member1 = await SeedMemberAsync();
        var member2 = await SeedMemberAsync();

        await AddGLPairAsync("0101", MemberReceivableAccountId, 50m, member1,
                              "1000", IncomeAccountId, 50m, null);

        await AddGLPairAsync("0101", MemberReceivableAccountId, 80m, member2,
                              "1000", IncomeAccountId, 80m, null);

        var total = await _sut.GetTotalOutstandingAsync();

        Assert.Equal(130m, total);
    }

    [Fact]
    public async Task GetTotalOutstanding_ReducedByCredits()
    {
        var memberId = await SeedMemberAsync();

        await AddGLPairAsync("0101", MemberReceivableAccountId, 100m, memberId,
                              "1000", IncomeAccountId, 100m, null);

        // Payment reduces outstanding
        await AddGLPairAsync("0100", CashAccountId, 40m, memberId,
                              "0101", MemberReceivableAccountId, 40m, memberId);

        var total = await _sut.GetTotalOutstandingAsync();

        Assert.Equal(60m, total);
    }

    // --- GetByDateRangeAsync ---

    [Fact]
    public async Task GetByDateRange_ReturnsTransactionsWithinRange()
    {
        var memberId = await SeedMemberAsync();

        var jan = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var mar = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var jun = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        await AddGLPairOnDateAsync(memberId, jan, 50m);
        await AddGLPairOnDateAsync(memberId, mar, 80m);
        await AddGLPairOnDateAsync(memberId, jun, 30m);

        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc);

        var txns = await _sut.GetByDateRangeAsync(from, to);

        // 2 pairs = 4 transactions within Jan-Mar
        Assert.Equal(4, txns.Count);
        Assert.All(txns, t => Assert.InRange(t.Date, from, to));
    }

    [Fact]
    public async Task GetByDateRange_ExcludesTransactionsOutsideRange()
    {
        var memberId = await SeedMemberAsync();

        var outside = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        await AddGLPairOnDateAsync(memberId, outside, 100m);

        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var txns = await _sut.GetByDateRangeAsync(from, to);

        Assert.Empty(txns);
    }

    // --- GetBalanceTotalsAsync ---

    [Fact]
    public async Task GetBalanceTotals_ReturnsSumOfDebitsAndCredits()
    {
        var memberId = await SeedMemberAsync();

        await AddGLPairAsync("0101", MemberReceivableAccountId, 100m, memberId,
                              "1000", IncomeAccountId, 100m, null);

        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var (totalDebits, totalCredits) = await _sut.GetBalanceTotalsAsync(from, to);

        Assert.Equal(100m, totalDebits);
        Assert.Equal(100m, totalCredits);
    }

    [Fact]
    public async Task GetBalanceTotals_BalancedDouble_EntrySystem_DebitEqualsCredit()
    {
        var memberId = await SeedMemberAsync();

        // Fee accrual pair
        await AddGLPairAsync("0101", MemberReceivableAccountId, 50m, memberId,
                              "1000", IncomeAccountId, 50m, null);

        // Payment pair
        await AddGLPairAsync("0100", CashAccountId, 30m, memberId,
                              "0101", MemberReceivableAccountId, 30m, memberId);

        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var (totalDebits, totalCredits) = await _sut.GetBalanceTotalsAsync(from, to);

        // In a balanced double-entry system, total debits always equal total credits
        Assert.Equal(totalDebits, totalCredits);
    }

    // --- AddPairAsync validation ---

    [Fact]
    public async Task AddPairAsync_ThrowsGLBalance_WhenAmountsDoNotMatch()
    {
        var memberId = await SeedMemberAsync();
        var date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await Assert.ThrowsAsync<Core.Exceptions.GLBalanceException>(() =>
            _sut.AddPairAsync(
                new Transaction
                {
                    Id = Guid.NewGuid(), Date = date, AccountId = MemberReceivableAccountId,
                    DebitAmount = 50m, CreditAmount = 0m, GLAccount = "0101",
                    MemberId = memberId, CreatedAt = date
                },
                new Transaction
                {
                    Id = Guid.NewGuid(), Date = date, AccountId = IncomeAccountId,
                    DebitAmount = 0m, CreditAmount = 40m, GLAccount = "1000",
                    MemberId = null, CreatedAt = date
                }));
    }

    // --- Helpers ---

    private async Task<Guid> SeedMemberAsync()
    {
        var member = new Member
        {
            Id = Guid.NewGuid(), Name = "Test Member", StreetAddress = "1 Test St",
            Status = MemberStatus.Active,
            ActivateDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            JoinDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync();
        return member.Id;
    }

    private async Task AddGLPairAsync(
        string debitAccount, Guid debitAccountId, decimal debitAmount, Guid? debitMemberId,
        string creditAccount, Guid creditAccountId, decimal creditAmount, Guid? creditMemberId)
    {
        var date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        await _sut.AddPairAsync(
            new Transaction
            {
                Id = Guid.NewGuid(), Date = date, AccountId = debitAccountId,
                DebitAmount = debitAmount, CreditAmount = 0m, GLAccount = debitAccount,
                MemberId = debitMemberId, CreatedAt = date
            },
            new Transaction
            {
                Id = Guid.NewGuid(), Date = date, AccountId = creditAccountId,
                DebitAmount = 0m, CreditAmount = creditAmount, GLAccount = creditAccount,
                MemberId = creditMemberId, CreatedAt = date
            });
    }

    private async Task AddGLPairOnDateAsync(Guid memberId, DateTime date, decimal amount)
    {
        await _sut.AddPairAsync(
            new Transaction
            {
                Id = Guid.NewGuid(), Date = date, AccountId = MemberReceivableAccountId,
                DebitAmount = amount, CreditAmount = 0m, GLAccount = "0101",
                MemberId = memberId, CreatedAt = date
            },
            new Transaction
            {
                Id = Guid.NewGuid(), Date = date, AccountId = IncomeAccountId,
                DebitAmount = 0m, CreditAmount = amount, GLAccount = "1000",
                MemberId = memberId, CreatedAt = date
            });
    }
}
