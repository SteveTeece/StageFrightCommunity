using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StageFright.Data.Repositories;

namespace StageFright.Data.Tests.Migrations;

/// <summary>
/// Integration tests for the ConvertCategoriesToAccounts migration: applies the
/// pre-expansion schema, inserts old-shape data (legacy category numbers, legacy
/// Transaction.GLAccount snapshots), migrates to latest, and asserts row counts,
/// GL balance, member balances, the renumber mapping, and that Transactions data
/// is byte-for-byte untouched.
/// </summary>
public sealed class ConvertCategoriesToAccountsMigrationTests : IDisposable
{
    private const string PreExpansionMigration = "20260627031057_AddShowParticipationGraphs";

    private static readonly Guid CashId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ReceivableId = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid BadDebtId = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid IncomeCat1Id = new("11111111-0000-0000-0000-000000000001");
    private static readonly Guid IncomeCat2Id = new("11111111-0000-0000-0000-000000000002");
    private static readonly Guid ExpenseCatId = new("11111111-0000-0000-0000-000000000003");
    private static readonly Guid MemberId = new("22222222-0000-0000-0000-000000000001");

    private readonly SqliteConnection _connection;

    public ConvertCategoriesToAccountsMigrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private StageFrightDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new StageFrightDbContext(options);
    }

    /// <summary>Migrates to the pre-expansion schema and inserts legacy-shape rows.</summary>
    private async Task SeedPreExpansionDatabaseAsync()
    {
        using var db = CreateContext();
        await db.GetService<IMigrator>().MigrateAsync(PreExpansionMigration);

        // User categories in the legacy count-based ranges.
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO Categories (Id, Name, Type, GLAccount, SortOrder, IsSystem, IsDeleted, DeletedAt, DeletedBy, CreatedAt, UpdatedAt) VALUES
            ({IncomeCat1Id}, 'Membership Fees', 'Income', '1000', 0, 0, 0, NULL, NULL, '2026-02-01 00:00:00', '2026-02-01 00:00:00'),
            ({IncomeCat2Id}, 'Concert Income', 'Income', '1001', 1, 0, 0, NULL, NULL, '2026-02-02 00:00:00', '2026-02-02 00:00:00'),
            ({ExpenseCatId}, 'Hall Hire', 'Expense', '2000', 0, 0, 0, NULL, NULL, '2026-02-03 00:00:00', '2026-02-03 00:00:00');
            """);

        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO Members (Id, Name, StreetAddress, Phone, Email, JoinDate, DateOfBirth, Status, ActivateDate, InactivateDate, IsDeleted, DeletedAt, DeletedBy, CreatedAt, UpdatedAt) VALUES
            ({MemberId}, 'Alice Legacy', '1 Old St', NULL, NULL, '2025-01-01 00:00:00', NULL, 'Active', NULL, NULL, 0, NULL, NULL, '2025-01-01 00:00:00', '2025-01-01 00:00:00');
            """);

        // Legacy GL: $100 fee accrual (DR 0101 / CR 1000) and $50 payment (DR 0100 / CR 0101).
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO Transactions (Id, Date, CategoryId, DebitAmount, CreditAmount, GLAccount, MemberId, PaymentId, FeeId, Description, CreatedAt) VALUES
            ('33333333-0000-0000-0000-000000000001', '2026-01-10 00:00:00', {ReceivableId}, 100.0, 0.0, '0101', {MemberId}, NULL, NULL, 'Annual fee', '2026-01-10 00:00:00'),
            ('33333333-0000-0000-0000-000000000002', '2026-01-10 00:00:00', {IncomeCat1Id}, 0.0, 100.0, '1000', NULL, NULL, NULL, 'Annual fee income', '2026-01-10 00:00:00'),
            ('33333333-0000-0000-0000-000000000003', '2026-01-20 00:00:00', {CashId}, 50.0, 0.0, '0100', {MemberId}, NULL, NULL, 'Payment', '2026-01-20 00:00:00'),
            ('33333333-0000-0000-0000-000000000004', '2026-01-20 00:00:00', {ReceivableId}, 0.0, 50.0, '0101', {MemberId}, NULL, NULL, 'Receivable cleared', '2026-01-20 00:00:00');
            """);

        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Settings (Id, OrganizationName, AnnualFee, AttendanceFee, MembershipRenewalMonth, CommitteeRenewalMonth, MaxAgeRangeYears, MinimumMemberAge, Theme, ShowParticipationGraphs, LastCommitteeResetYear, SchemaVersion, IsDeleted, DeletedAt, DeletedBy, CreatedAt, UpdatedAt) VALUES
            ('44444444-0000-0000-0000-000000000001', 'Legacy Org', 100.0, 10.0, 1, 1, 150, 0, 'Light', 1, NULL, '1.0.0', 0, NULL, NULL, '2025-01-01 00:00:00', '2025-01-01 00:00:00');
            """);
    }

    private async Task MigrateToLatestAsync()
    {
        using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    [Fact]
    public async Task Should_PreserveRowCountsAndAddSystemAccounts_When_Migrated()
    {
        await SeedPreExpansionDatabaseAsync();
        await MigrateToLatestAsync();

        using var db = CreateContext();
        var accountCount = await db.Accounts.IgnoreQueryFilters().CountAsync();
        var transactionCount = await db.Transactions.CountAsync();

        Assert.Equal(10, accountCount);      // 3 system + 3 user + 4 new system
        Assert.Equal(4, transactionCount);   // untouched
    }

    [Fact]
    public async Task Should_KeepGLBalanced_When_Migrated()
    {
        await SeedPreExpansionDatabaseAsync();
        await MigrateToLatestAsync();

        using var db = CreateContext();
        var debits = await db.Transactions.SumAsync(t => t.DebitAmount);
        var credits = await db.Transactions.SumAsync(t => t.CreditAmount);

        Assert.Equal(150m, debits);
        Assert.Equal(debits, credits);
    }

    [Fact]
    public async Task Should_KeepMemberBalanceIdentical_When_Migrated()
    {
        await SeedPreExpansionDatabaseAsync();
        await MigrateToLatestAsync();

        using var db = CreateContext();
        var gl = new GLRepository(db);

        // Pre-migration: 100 accrued − 50 paid = 50 outstanding.
        Assert.Equal(50m, await gl.GetMemberBalanceAsync(MemberId));
        Assert.Equal(50m, await gl.GetTotalOutstandingAsync());
    }

    [Fact]
    public async Task Should_RenumberAccountsIntoAuRanges_When_Migrated()
    {
        await SeedPreExpansionDatabaseAsync();
        await MigrateToLatestAsync();

        using var db = CreateContext();
        var byId = await db.Accounts.IgnoreQueryFilters().ToDictionaryAsync(a => a.Id);

        Assert.Equal("1100", byId[CashId].AccountNumber);
        Assert.True(byId[CashId].IsBankAccount);
        Assert.Equal("1200", byId[ReceivableId].AccountNumber);
        Assert.Equal("6999", byId[BadDebtId].AccountNumber);
        Assert.Equal("4000", byId[IncomeCat1Id].AccountNumber);
        Assert.Equal("4001", byId[IncomeCat2Id].AccountNumber);
        Assert.Equal("6000", byId[ExpenseCatId].AccountNumber);
    }

    [Fact]
    public async Task Should_LeaveTransactionGLAccountSnapshotsUntouched_When_Migrated()
    {
        await SeedPreExpansionDatabaseAsync();
        await MigrateToLatestAsync();

        using var db = CreateContext();
        var snapshots = await db.Transactions
            .OrderBy(t => t.CreatedAt).ThenBy(t => t.GLAccount)
            .Select(t => t.GLAccount)
            .ToListAsync();

        Assert.Equal(new[] { "0101", "1000", "0100", "0101" }.OrderBy(s => s), snapshots.OrderBy(s => s));
    }

    [Fact]
    public async Task Should_SetFinancialYearDefaultAndBumpSchemaVersion_When_Migrated()
    {
        await SeedPreExpansionDatabaseAsync();
        await MigrateToLatestAsync();

        using var db = CreateContext();
        var settings = await db.Settings.SingleAsync();

        Assert.Equal(7, settings.FinancialYearStartMonth);
        Assert.Equal("1.1.0", settings.SchemaVersion);
    }

    [Fact]
    public async Task Should_LinkTransactionsToRenumberedAccounts_When_Migrated()
    {
        await SeedPreExpansionDatabaseAsync();
        await MigrateToLatestAsync();

        using var db = CreateContext();

        // AccountId FKs survive the column rename and still join to the renamed table.
        var incomeLeg = await db.Transactions.SingleAsync(t => t.GLAccount == "1000");
        Assert.Equal(IncomeCat1Id, incomeLeg.AccountId);

        var account = await db.Accounts.SingleAsync(a => a.Id == incomeLeg.AccountId);
        Assert.Equal("4000", account.AccountNumber);
    }
}
