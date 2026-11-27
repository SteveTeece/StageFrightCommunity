using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V36 (spec 033, User Story 4): a cash withdrawal lowers the bank and raises Petty Cash,
/// shows in the Banking history with its purpose, reconciles as one line, is refused for a bad amount, a bad bank
/// or a closed period, writes an audit entry, and a float banked back leaves Petty Cash where it started.
/// Real in-memory SQLite with the full EF migration set and a real audit trail repository.
/// </summary>
public sealed class V36_CashWithdrawalTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid IncomeId = Guid.NewGuid();
    private static readonly Guid BankId = Guid.NewGuid();
    private static readonly DateTime Day1 = new(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = new(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day3 = new(2026, 3, 6, 0, 0, 0, DateTimeKind.Utc);

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        var now = DateTime.UtcNow;
        _db.Accounts.AddRange(
            new Account { Id = IncomeId, Name = "Ticket Sales", Type = AccountType.Income, AccountNumber = "4000", SortOrder = 0, IsSystem = false, CreatedAt = now, UpdatedAt = now },
            new Account { Id = BankId, Name = "Community Bank", Type = AccountType.Asset, AccountNumber = "1110", IsBankAccount = true, SortOrder = 0, IsSystem = false, CreatedAt = now, UpdatedAt = now });
        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Test Choir", AnnualFee = 50m, AttendanceFee = 10m,
            MembershipRenewalMonth = 1, MaxAgeRangeYears = 150, MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            FinancialYearStartMonth = 7, FinancialYearStartDay = 1, CurrencyCode = "AUD", IsTaxApplicable = false,
            CreatedAt = now, UpdatedAt = now
        });
        await _db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- FR-014, US4-1: posting and history ----------------------------------------------

    [Fact]
    public async Task Should_LowerBankAndRaisePettyCashAndListWithPurpose_When_FloatWithdrawn_Integration()
    {
        await Banking().RecordWithdrawalAsync(
            new RecordCashWithdrawalRequest { Date = Day1, BankAccountId = BankId, Amount = 200m, Purpose = "Opening night float" },
            TestContext.Current.CancellationToken);

        Assert.Equal(-200m, await BalanceAsync(BankId));
        Assert.Equal(200m, await BalanceAsync(SystemAccounts.CashId));
        var history = await Banking().GetHistoryAsync(TestContext.Current.CancellationToken);
        var row = Assert.Single(history);
        Assert.Equal(BankingHistoryEntryType.Withdrawal, row.Type);
        Assert.Equal(Day1, row.Date);
        Assert.Equal("Community Bank", row.BankAccountName);
        Assert.Equal(200m, row.Total);
        Assert.Equal("Opening night float", row.Description);
        Assert.False(row.IsReversed);
    }

    // --- US4-2: refusals -----------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Should_RefuseAndPostNothing_When_AmountIsZeroOrNegative_Integration(int amount)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => Banking().RecordWithdrawalAsync(
            new RecordCashWithdrawalRequest { Date = Day1, BankAccountId = BankId, Amount = amount },
            TestContext.Current.CancellationToken));

        Assert.Equal("The withdrawal amount must be greater than zero.", ex.Message);
        Assert.Empty(await _db.Transactions.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_RefuseAndPostNothing_When_NoBankAccountChosen_Integration()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => Banking().RecordWithdrawalAsync(
            new RecordCashWithdrawalRequest { Date = Day1, BankAccountId = Guid.Empty, Amount = 50m },
            TestContext.Current.CancellationToken));

        Assert.Equal("Choose a bank account other than Petty Cash.", ex.Message);
        Assert.Empty(await _db.Transactions.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_RefuseAndPostNothing_When_PettyCashChosenAsBank_Integration()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Banking().RecordWithdrawalAsync(
            new RecordCashWithdrawalRequest { Date = Day1, BankAccountId = SystemAccounts.CashId, Amount = 50m },
            TestContext.Current.CancellationToken));

        Assert.Empty(await _db.Transactions.ToListAsync(TestContext.Current.CancellationToken));
    }

    // --- US4-3: reconciliation -----------------------------------------------------------

    [Fact]
    public async Task Should_ShowOneReconciliationLine_When_WithdrawalReconciled_Integration()
    {
        await Banking().RecordWithdrawalAsync(
            new RecordCashWithdrawalRequest { Date = Day1, BankAccountId = BankId, Amount = 150m, Purpose = "Bar float" },
            TestContext.Current.CancellationToken);

        var service = new BankReconciliationService(new BankReconciliationRepository(_db), new AccountRepository(_db), Gl(), Audit(), RealLocalizer.Instance);
        var draft = await service.StartDraftAsync(
            new StartReconciliationRequest { AccountId = BankId, StatementDate = Day3, StatementClosingBalance = -150m },
            TestContext.Current.CancellationToken);
        var workspace = await service.GetWorkspaceAsync(draft.Id, TestContext.Current.CancellationToken);

        var line = Assert.Single(workspace.Transactions);
        Assert.Equal(150m, line.Transaction.CreditAmount);
    }

    // --- FR-018, FR-019: closed period and audit -----------------------------------------

    [Fact]
    public async Task Should_RefuseAndPostNothing_When_DatedInClosedPeriod_Integration()
    {
        (await _db.Settings.SingleAsync(TestContext.Current.CancellationToken)).ClosedThroughDate = Day1;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ClosedPeriodException>(() => Banking().RecordWithdrawalAsync(
            new RecordCashWithdrawalRequest { Date = Day1, BankAccountId = BankId, Amount = 50m },
            TestContext.Current.CancellationToken));

        Assert.Empty(await _db.Transactions.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _db.JournalEntries.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_KeepBooksBalancedAndWriteAuditEntry_When_WithdrawalRecorded_Integration()
    {
        var entry = await Banking().RecordWithdrawalAsync(
            new RecordCashWithdrawalRequest { Date = Day1, BankAccountId = BankId, Amount = 75m },
            TestContext.Current.CancellationToken);

        Assert.Equal(JournalEntryType.CashWithdrawal, entry.Type);
        Assert.Null(entry.ExternalOrganisationId); // FR-025
        var transactions = await _db.Transactions.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, transactions.Count);
        Assert.Equal(transactions.Sum(t => t.DebitAmount), transactions.Sum(t => t.CreditAmount));
        var audit = await _db.AuditTrailEntries.SingleAsync(
            a => a.EntityType == nameof(JournalEntry) && a.EntityId == entry.Id, TestContext.Current.CancellationToken);
        Assert.Equal(AuditAction.Create, audit.Action);
    }

    // --- FR-017, verification step 4: the float comes back -------------------------------

    [Fact]
    public async Task Should_LeavePettyCashAtTakings_When_FloatBankedBackWithDeposit_Integration()
    {
        await Banking().RecordWithdrawalAsync(
            new RecordCashWithdrawalRequest { Date = Day1, BankAccountId = BankId, Amount = 200m, Purpose = "Concert float" },
            TestContext.Current.CancellationToken);
        await BuildIncomeService().RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = Day2, Amount = 350m, AccountId = IncomeId, PaymentMethod = PaymentMethod.Cash
        }, TestContext.Current.CancellationToken);
        Assert.Equal(550m, await BalanceAsync(SystemAccounts.CashId));

        // Bank the float back, keep the takings in the tin.
        await Banking().RecordDepositAsync(
            new RecordDepositRequest { Date = Day3, BankAccountId = BankId, CashAmount = 200m },
            TestContext.Current.CancellationToken);

        Assert.Equal(350m, await BalanceAsync(SystemAccounts.CashId));
        Assert.Equal(0m, await BalanceAsync(BankId));
        var history = await Banking().GetHistoryAsync(TestContext.Current.CancellationToken);
        Assert.Equal([BankingHistoryEntryType.Deposit, BankingHistoryEntryType.Withdrawal], history.Select(h => h.Type));
    }

    [Fact]
    public async Task Should_RestoreBankAndPettyCash_When_MistakenWithdrawalDepositedBackOnSameDate_Integration()
    {
        await BuildIncomeService().RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = Day1, Amount = 30m, AccountId = IncomeId, PaymentMethod = PaymentMethod.Cash
        }, TestContext.Current.CancellationToken);
        await Banking().RecordWithdrawalAsync(
            new RecordCashWithdrawalRequest { Date = Day2, BankAccountId = BankId, Amount = 500m, Purpose = "Typed wrongly" },
            TestContext.Current.CancellationToken);

        // FR-017: the correction is a deposit of that cash back into the same bank, dated the same day.
        await Banking().RecordDepositAsync(
            new RecordDepositRequest { Date = Day2, BankAccountId = BankId, CashAmount = 500m },
            TestContext.Current.CancellationToken);

        Assert.Equal(30m, await BalanceAsync(SystemAccounts.CashId));
        Assert.Equal(0m, await BalanceAsync(BankId));
    }

    // --- Helpers -------------------------------------------------------------------------

    // Debits minus credits on an account.
    private async Task<decimal> BalanceAsync(Guid accountId) =>
        await _db.Transactions.Where(t => t.AccountId == accountId).SumAsync(t => t.DebitAmount - t.CreditAmount, TestContext.Current.CancellationToken);

    private GLRepository Gl() => new(_db, new ClosedPeriodGuard(new SettingsRepository(_db)));

    // A real audit repository so the audit entry is actually written (FR-019).
    private AuditTrailService Audit() =>
        new(new AuditTrailRepository(_db), NullLogger<AuditTrailService>.Instance);

    private IncomeEntryService BuildIncomeService() =>
        new(new AccountRepository(_db), Gl(), new JournalEntryRepository(_db), new SettingsRepository(_db), Audit(), new UnitOfWork(_db), RealLocalizer.Instance, new ExternalOrganisationRepository(_db), new ReceivedItemRepository(_db));

    private BankingService Banking() =>
        new(new AccountRepository(_db), Gl(), new JournalEntryRepository(_db), new ReceivedItemRepository(_db), new BankDepositRepository(_db),
            new BankReconciliationRepository(_db), new ClosedPeriodGuard(new SettingsRepository(_db)), Audit(), new UnitOfWork(_db), RealLocalizer.Instance);
}
