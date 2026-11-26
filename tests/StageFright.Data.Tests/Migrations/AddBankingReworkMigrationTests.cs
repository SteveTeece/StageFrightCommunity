using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;

namespace StageFright.Data.Tests.Migrations;

/// <summary>
/// Integration tests for the AddBankingRework migration (spec 033): Cash on Hand becomes Petty Cash
/// with its number and history intact (FR-002, US2-1), Undeposited Funds 1105 is seeded as a system
/// non-bank asset (FR-001), the three banking tables exist with the one-source check, and Down reverses it.
/// </summary>
public sealed class AddBankingReworkMigrationTests : IDisposable
{
    private const string PreMigration = "20261119195813_AddExternalOrganisations";

    private static readonly Guid TransactionId = new("33033033-0000-0000-0000-000000000001");
    private static readonly Guid JournalEntryId = new("33033033-0000-0000-0000-000000000002");

    private readonly SqliteConnection _connection;

    public AddBankingReworkMigrationTests()
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

    private static async Task<object?> ScalarAsync(StageFrightDbContext db, string sql)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private static Task<object?> CountBankingTablesAsync(StageFrightDbContext db) => ScalarAsync(db, """
        SELECT COUNT(*) FROM sqlite_master WHERE type = 'table'
        AND name IN ('ReceivedItems', 'BankDeposits', 'BankDepositLines');
        """);

    private static Task InsertIncomeEntryAsync(StageFrightDbContext db) =>
        db.Database.ExecuteSqlAsync($"""
            INSERT INTO JournalEntries (Id, Type, Date, CreatedAt) VALUES
            ({JournalEntryId}, 'Income', '2026-10-01 00:00:00', '2026-10-01 00:00:00');
            """, cancellationToken: TestContext.Current.CancellationToken);

    [Fact]
    public async Task Should_RenameCashOnHandToPettyCash_KeepingNumberAndRows_When_Migrated_Integration()
    {
        using (var db = CreateContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(PreMigration, TestContext.Current.CancellationToken);

            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO Transactions (Id, Date, AccountId, DebitAmount, CreditAmount, GLAccount, CreatedAt) VALUES
                ({TransactionId}, '2026-10-01 00:00:00', {SystemAccounts.CashId}, '25.00', '0', 'Cash on Hand', '2026-10-01 00:00:00');
                """, cancellationToken: TestContext.Current.CancellationToken);
        }

        using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

            var cash = await db.Accounts.AsNoTracking()
                .SingleAsync(a => a.Id == SystemAccounts.CashId, TestContext.Current.CancellationToken);
            Assert.Equal("Petty Cash", cash.Name);
            Assert.Equal(SystemAccounts.CashNumber, cash.AccountNumber);
            Assert.True(cash.IsBankAccount);

            var row = await db.Transactions.AsNoTracking()
                .SingleAsync(t => t.Id == TransactionId, TestContext.Current.CancellationToken);
            Assert.Equal(SystemAccounts.CashId, row.AccountId);
            Assert.Equal(25.00m, row.DebitAmount);
        }
    }

    [Fact]
    public async Task Should_RenameOwnPettyCashAccount_When_NameClashesWithSystemAccount_Integration()
    {
        var ownAccountId = new Guid("33033033-0000-0000-0000-000000000003");
        using (var db = CreateContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(PreMigration, TestContext.Current.CancellationToken);

            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO Accounts (Id, Name, Type, AccountNumber, SortOrder, IsSystem, IsBankAccount, IsDeleted, CreatedAt, UpdatedAt) VALUES
                ({ownAccountId}, 'petty cash', 'Asset', '1110', 0, 0, 1, 0, '2026-10-01 00:00:00', '2026-10-01 00:00:00');
                """, cancellationToken: TestContext.Current.CancellationToken);
        }

        using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

            var own = await db.Accounts.AsNoTracking()
                .SingleAsync(a => a.Id == ownAccountId, TestContext.Current.CancellationToken);
            Assert.Equal("Petty Cash (own)", own.Name);
            Assert.Equal("1110", own.AccountNumber);
            Assert.Equal(1, await db.Accounts.CountAsync(a => a.Name == "Petty Cash", TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Should_SeedUndepositedFundsAsSystemNonBankAsset_When_Migrated_Integration()
    {
        using var db = CreateContext();
        await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

        var account = await db.Accounts.AsNoTracking()
            .SingleAsync(a => a.Id == SystemAccounts.UndepositedFundsId, TestContext.Current.CancellationToken);

        Assert.Equal(SystemAccounts.UndepositedFundsNumber, account.AccountNumber);
        Assert.Equal("Undeposited Funds", account.Name);
        Assert.Equal(AccountType.Asset, account.Type);
        Assert.True(account.IsSystem);
        Assert.False(account.IsBankAccount);
        Assert.False(account.IsDeleted);
    }

    [Fact]
    public async Task Should_CreateBankingTables_When_Migrated_Integration()
    {
        using var db = CreateContext();
        await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3L, await CountBankingTablesAsync(db));
    }

    [Fact]
    public async Task Should_RefuseReceivedItem_When_NoSourceSet_Integration()
    {
        using var db = CreateContext();
        await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlAsync($"""
            INSERT INTO ReceivedItems (Id, Date, Amount, Method, CreatedAt) VALUES
            ({Guid.NewGuid()}, '2026-10-01 00:00:00', '10.00', 'Check', '2026-10-01 00:00:00');
            """, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("CK_ReceivedItems_ExactlyOneSource", ex.Message);
    }

    [Fact]
    public async Task Should_AcceptReceivedItem_When_OnlyJournalEntrySet_Integration()
    {
        using var db = CreateContext();
        await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
        await InsertIncomeEntryAsync(db);

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO ReceivedItems (Id, Date, Amount, Method, JournalEntryId, CreatedAt) VALUES
            ({Guid.NewGuid()}, '2026-10-01 00:00:00', '10.00', 'Check', {JournalEntryId}, '2026-10-01 00:00:00');
            """, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, await db.ReceivedItems.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_RestoreCashOnHandAndDropBanking_When_MigratedDown_Integration()
    {
        using var db = CreateContext();
        await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

        await db.GetService<IMigrator>().MigrateAsync(PreMigration, TestContext.Current.CancellationToken);

        Assert.Equal("Cash on Hand", await ScalarAsync(db,
            "SELECT Name FROM Accounts WHERE AccountNumber = '1100';"));
        Assert.Equal(0L, await ScalarAsync(db,
            "SELECT COUNT(*) FROM Accounts WHERE AccountNumber = '1105';"));
        Assert.Equal(0L, await CountBankingTablesAsync(db));
    }
}
