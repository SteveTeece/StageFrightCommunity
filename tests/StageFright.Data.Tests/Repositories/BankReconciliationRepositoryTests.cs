using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Data.Repositories;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for BankReconciliationRepository and GLRepository.GetUnreconciledByAccountAsync
/// against a real SQLite in-memory database: draft creation with chained opening balance,
/// draft-only line mutation and deletion, finalised immutability, the unique
/// (ReconciliationId, TransactionId) index, and cleared/unreconciled lookups.
/// </summary>
public sealed class BankReconciliationRepositoryTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;
    private BankReconciliationRepository _sut = null!;
    private GLRepository _glRepo = null!;

    private static readonly Guid IncomeAccountId = new("00000000-0000-0000-0000-000000000030");

    private static readonly DateTime June30 = new(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime July31 = new(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc);

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
            Id = IncomeAccountId, Name = "Raffle Income", Type = AccountType.Income,
            AccountNumber = "4000", SortOrder = 0, IsSystem = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _sut = new BankReconciliationRepository(_db);
        _glRepo = new GLRepository(_db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- CreateDraftAsync ---

    [Fact]
    public async Task Should_CreateDraftWithZeroOpeningBalance_When_FirstReconciliationForAccount_Integration()
    {
        var draft = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 500m, "first rec", TestContext.Current.CancellationToken);

        Assert.Equal(ReconciliationStatus.Draft, draft.Status);
        Assert.Equal(0m, draft.OpeningBalance);
        Assert.Equal(500m, draft.StatementClosingBalance);
        Assert.Equal("first rec", draft.Notes);
        Assert.Null(draft.FinalisedAt);
    }

    [Fact]
    public async Task Should_ChainOpeningBalanceFromLastFinalised_When_PreviousReconciliationExists_Integration()
    {
        var first = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 500m, null, TestContext.Current.CancellationToken);
        await _sut.FinaliseAsync(first.Id, TestContext.Current.CancellationToken);

        var second = await _sut.CreateDraftAsync(SystemAccounts.CashId, July31, 750m, null, TestContext.Current.CancellationToken);

        Assert.Equal(500m, second.OpeningBalance);
    }

    [Fact]
    public async Task Should_NotChainOpeningBalanceFromDraft_When_PreviousReconciliationIsUnfinalised_Integration()
    {
        // A soft-deleted draft must not feed the opening balance either.
        var abandoned = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 999m, null, TestContext.Current.CancellationToken);
        await _sut.SoftDeleteDraftAsync(abandoned.Id, "test", TestContext.Current.CancellationToken);

        var next = await _sut.CreateDraftAsync(SystemAccounts.CashId, July31, 750m, null, TestContext.Current.CancellationToken);

        Assert.Equal(0m, next.OpeningBalance);
    }

    // --- Lines: draft-only mutation ---

    [Fact]
    public async Task Should_AddAndRemoveLine_When_ReconciliationIsDraft_Integration()
    {
        var draft = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 100m, null, TestContext.Current.CancellationToken);
        var txId = await SeedBankPairAsync(100m, June30.AddDays(-5));

        await _sut.AddLineAsync(draft.Id, txId, TestContext.Current.CancellationToken);
        Assert.Equal(1, await _db.ReconciliationLines.CountAsync(cancellationToken: TestContext.Current.CancellationToken));

        await _sut.RemoveLineAsync(draft.Id, txId, TestContext.Current.CancellationToken);
        Assert.Equal(0, await _db.ReconciliationLines.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowReconciliationException_When_AddingLineToFinalisedReconciliation_Integration()
    {
        var rec = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 0m, null, TestContext.Current.CancellationToken);
        await _sut.FinaliseAsync(rec.Id, TestContext.Current.CancellationToken);
        var txId = await SeedBankPairAsync(50m, June30.AddDays(-5));

        await Assert.ThrowsAsync<ReconciliationException>(() => _sut.AddLineAsync(rec.Id, txId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowReconciliationException_When_RemovingLineFromFinalisedReconciliation_Integration()
    {
        var rec = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 100m, null, TestContext.Current.CancellationToken);
        var txId = await SeedBankPairAsync(100m, June30.AddDays(-5));
        await _sut.AddLineAsync(rec.Id, txId, TestContext.Current.CancellationToken);
        await _sut.FinaliseAsync(rec.Id, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ReconciliationException>(() => _sut.RemoveLineAsync(rec.Id, txId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowDataAccess_When_SameTransactionAddedTwiceToOneReconciliation_Integration()
    {
        var draft = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 100m, null, TestContext.Current.CancellationToken);
        var txId = await SeedBankPairAsync(100m, June30.AddDays(-5));
        await _sut.AddLineAsync(draft.Id, txId, TestContext.Current.CancellationToken);

        // Unique (ReconciliationId, TransactionId) index rejects the duplicate.
        await Assert.ThrowsAsync<DataAccessException>(() => _sut.AddLineAsync(draft.Id, txId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowEntityNotFound_When_AddingLineToUnknownReconciliation_Integration()
    {
        var txId = await SeedBankPairAsync(50m, June30);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => _sut.AddLineAsync(Guid.NewGuid(), txId, TestContext.Current.CancellationToken));
    }

    // --- Finalise / delete lifecycle ---

    [Fact]
    public async Task Should_SetStatusAndFinalisedAt_When_Finalising_Integration()
    {
        var draft = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 0m, null, TestContext.Current.CancellationToken);

        await _sut.FinaliseAsync(draft.Id, TestContext.Current.CancellationToken);

        var reloaded = await _sut.GetByIdAsync(draft.Id, TestContext.Current.CancellationToken);
        Assert.Equal(ReconciliationStatus.Finalised, reloaded!.Status);
        Assert.NotNull(reloaded.FinalisedAt);
    }

    [Fact]
    public async Task Should_ThrowReconciliationException_When_FinalisingTwice_Integration()
    {
        var draft = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 0m, null, TestContext.Current.CancellationToken);
        await _sut.FinaliseAsync(draft.Id, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ReconciliationException>(() => _sut.FinaliseAsync(draft.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_SoftDeleteDraft_When_Deleting_Integration()
    {
        var draft = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 0m, null, TestContext.Current.CancellationToken);

        await _sut.SoftDeleteDraftAsync(draft.Id, "test-user", TestContext.Current.CancellationToken);

        Assert.Null(await _sut.GetByIdAsync(draft.Id, TestContext.Current.CancellationToken));
        var raw = await _db.BankReconciliations.IgnoreQueryFilters().SingleAsync(r => r.Id == draft.Id, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(raw.IsDeleted);
        Assert.Equal("test-user", raw.DeletedBy);
        Assert.NotNull(raw.DeletedAt);
    }

    [Fact]
    public async Task Should_ThrowReconciliationException_When_DeletingFinalisedReconciliation_Integration()
    {
        var rec = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 0m, null, TestContext.Current.CancellationToken);
        await _sut.FinaliseAsync(rec.Id, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ReconciliationException>(() => _sut.SoftDeleteDraftAsync(rec.Id, "test", TestContext.Current.CancellationToken));
    }

    // --- Lookups ---

    [Fact]
    public async Task Should_ReturnDraft_When_AccountHasOne_Integration()
    {
        var draft = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 0m, null, TestContext.Current.CancellationToken);

        var found = await _sut.GetDraftForAccountAsync(SystemAccounts.CashId, TestContext.Current.CancellationToken);

        Assert.Equal(draft.Id, found!.Id);
        Assert.Null(await _sut.GetDraftForAccountAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ReturnNewestStatementFirst_When_GettingHistory_Integration()
    {
        var first = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 0m, null, TestContext.Current.CancellationToken);
        await _sut.FinaliseAsync(first.Id, TestContext.Current.CancellationToken);
        var second = await _sut.CreateDraftAsync(SystemAccounts.CashId, July31, 0m, null, TestContext.Current.CancellationToken);

        var history = await _sut.GetByAccountAsync(SystemAccounts.CashId, TestContext.Current.CancellationToken);

        Assert.Equal(2, history.Count);
        Assert.Equal(second.Id, history[0].Id);
        Assert.Equal(first.Id, history[1].Id);
    }

    [Fact]
    public async Task Should_DetectClearedElsewhere_When_TransactionBelongsToAnotherReconciliation_Integration()
    {
        var rec = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 100m, null, TestContext.Current.CancellationToken);
        var txId = await SeedBankPairAsync(100m, June30.AddDays(-5));
        await _sut.AddLineAsync(rec.Id, txId, TestContext.Current.CancellationToken);

        Assert.True(await _sut.IsTransactionClearedElsewhereAsync(txId, Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.False(await _sut.IsTransactionClearedElsewhereAsync(txId, rec.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_IgnoreDeletedReconciliations_When_CheckingClearedElsewhere_Integration()
    {
        var rec = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 100m, null, TestContext.Current.CancellationToken);
        var txId = await SeedBankPairAsync(100m, June30.AddDays(-5));
        await _sut.AddLineAsync(rec.Id, txId, TestContext.Current.CancellationToken);
        await _sut.SoftDeleteDraftAsync(rec.Id, "test", TestContext.Current.CancellationToken);

        Assert.False(await _sut.IsTransactionClearedElsewhereAsync(txId, Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    // --- GLRepository.GetUnreconciledByAccountAsync ---

    [Fact]
    public async Task Should_ExcludeClearedTransactions_When_GettingUnreconciled_Integration()
    {
        var clearedId = await SeedBankPairAsync(100m, June30.AddDays(-10));
        var unclearedId = await SeedBankPairAsync(50m, June30.AddDays(-5));
        var rec = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 100m, null, TestContext.Current.CancellationToken);
        await _sut.AddLineAsync(rec.Id, clearedId, TestContext.Current.CancellationToken);

        var unreconciled = await _glRepo.GetUnreconciledByAccountAsync(SystemAccounts.CashId, ct: TestContext.Current.CancellationToken);

        var tx = Assert.Single(unreconciled);
        Assert.Equal(unclearedId, tx.Id);
    }

    [Fact]
    public async Task Should_ReleaseClearedTransactions_When_ReconciliationIsSoftDeleted_Integration()
    {
        var txId = await SeedBankPairAsync(100m, June30.AddDays(-10));
        var rec = await _sut.CreateDraftAsync(SystemAccounts.CashId, June30, 100m, null, TestContext.Current.CancellationToken);
        await _sut.AddLineAsync(rec.Id, txId, TestContext.Current.CancellationToken);
        await _sut.SoftDeleteDraftAsync(rec.Id, "test", TestContext.Current.CancellationToken);

        var unreconciled = await _glRepo.GetUnreconciledByAccountAsync(SystemAccounts.CashId, ct: TestContext.Current.CancellationToken);

        Assert.Contains(unreconciled, t => t.Id == txId);
    }

    [Fact]
    public async Task Should_LimitToDateAndAccount_When_GettingUnreconciledWithUpTo_Integration()
    {
        await SeedBankPairAsync(100m, June30.AddDays(-10));
        await SeedBankPairAsync(75m, July31); // after the cut-off

        var unreconciled = await _glRepo.GetUnreconciledByAccountAsync(SystemAccounts.CashId, June30, TestContext.Current.CancellationToken);

        var tx = Assert.Single(unreconciled);
        Assert.Equal(100m, tx.DebitAmount);
        Assert.Empty(await _glRepo.GetUnreconciledByAccountAsync(IncomeAccountId, June30.AddDays(-30), TestContext.Current.CancellationToken));
    }

    // --- Helpers ---

    /// <summary>Posts DR Cash / CR Income and returns the bank-side (Cash) transaction id.</summary>
    private async Task<Guid> SeedBankPairAsync(decimal amount, DateTime date)
    {
        var bankTxId = Guid.NewGuid();
        _db.Transactions.AddRange(
            new Transaction
            {
                Id = bankTxId, Date = date, AccountId = SystemAccounts.CashId,
                DebitAmount = amount, CreditAmount = 0m, GLAccount = SystemAccounts.CashNumber,
                CreatedAt = date
            },
            new Transaction
            {
                Id = Guid.NewGuid(), Date = date, AccountId = IncomeAccountId,
                DebitAmount = 0m, CreditAmount = amount, GLAccount = "4000",
                CreatedAt = date
            });
        await _db.SaveChangesAsync();
        return bankTxId;
    }
}
