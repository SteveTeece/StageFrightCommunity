using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Data.Repositories;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for the two reconciliation queries the deposit reversal relies on (spec 033 research §9):
/// <see cref="BankReconciliationRepository.IsTransactionInFinalisedAsync"/> and
/// <see cref="BankReconciliationRepository.GetDraftLineReconciliationIdAsync"/> tell finalised lines from
/// draft lines, and ignore a soft-deleted draft.
/// </summary>
public sealed class BankReconciliationRepositoryFinalisedTests : IAsyncLifetime
{
    private static readonly Guid IncomeAccountId = new("00000000-0000-0000-0000-000000000030");
    private static readonly DateTime June30 = new(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

    private StageFrightDbContext _db = null!;
    private BankReconciliationRepository _sut = null!;

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
            Id = IncomeAccountId, Name = "Raffle Income", Type = AccountType.Income, AccountNumber = "4000",
            CreatedAt = June30, UpdatedAt = June30
        });
        await _db.SaveChangesAsync();

        _sut = new BankReconciliationRepository(_db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    private async Task<Guid> SeedCashLineAsync(decimal amount)
    {
        var cashTxId = Guid.NewGuid();
        _db.Transactions.AddRange(
            new Transaction
            {
                Id = cashTxId, Date = June30, AccountId = SystemAccounts.CashId, DebitAmount = amount,
                GLAccount = SystemAccounts.CashNumber, CreatedAt = June30
            },
            new Transaction
            {
                Id = Guid.NewGuid(), Date = June30, AccountId = IncomeAccountId, CreditAmount = amount,
                GLAccount = "4000", CreatedAt = June30
            });
        await _db.SaveChangesAsync();
        return cashTxId;
    }

    [Fact]
    public async Task Should_ReportNeither_When_TransactionNotCleared_Integration()
    {
        var txId = await SeedCashLineAsync(10m);

        Assert.False(await _sut.IsTransactionInFinalisedAsync(txId, TestContext.Current.CancellationToken));
        Assert.Null(await _sut.GetDraftLineReconciliationIdAsync(txId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ReturnDraftIdAndNotFinalised_When_ClearedOnDraft_Integration()
    {
        var txId = await SeedCashLineAsync(10m);
        var draft = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 10m, null, TestContext.Current.CancellationToken);
        await _sut.AddLineAsync(draft.Id, txId, TestContext.Current.CancellationToken);

        Assert.False(await _sut.IsTransactionInFinalisedAsync(txId, TestContext.Current.CancellationToken));
        Assert.Equal(draft.Id, await _sut.GetDraftLineReconciliationIdAsync(txId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ReportFinalisedAndNoDraft_When_ClearedOnFinalised_Integration()
    {
        var txId = await SeedCashLineAsync(10m);
        var rec = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 10m, null, TestContext.Current.CancellationToken);
        await _sut.AddLineAsync(rec.Id, txId, TestContext.Current.CancellationToken);
        await _sut.FinaliseAsync(rec.Id, TestContext.Current.CancellationToken);

        Assert.True(await _sut.IsTransactionInFinalisedAsync(txId, TestContext.Current.CancellationToken));
        Assert.Null(await _sut.GetDraftLineReconciliationIdAsync(txId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_IgnoreLine_When_DraftIsSoftDeleted_Integration()
    {
        var txId = await SeedCashLineAsync(10m);
        var draft = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 10m, null, TestContext.Current.CancellationToken);
        await _sut.AddLineAsync(draft.Id, txId, TestContext.Current.CancellationToken);
        await _sut.SoftDeleteDraftAsync(draft.Id, "test", TestContext.Current.CancellationToken);

        Assert.Null(await _sut.GetDraftLineReconciliationIdAsync(txId, TestContext.Current.CancellationToken));
        Assert.False(await _sut.IsTransactionInFinalisedAsync(txId, TestContext.Current.CancellationToken));
    }
}
