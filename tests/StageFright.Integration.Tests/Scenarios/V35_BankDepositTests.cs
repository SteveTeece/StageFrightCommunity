using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V35 (spec 033, User Story 3): a bank deposit of ticked cheques plus cash raises the bank
/// by the total, clears Undeposited Funds and Petty Cash, reconciles as one line, and is refused when empty,
/// over the cash available, already taken, back-dated before a cheque, or in a closed period.
/// Real in-memory SQLite with the full EF migration set and a real audit trail repository.
/// </summary>
public sealed class V35_BankDepositTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid IncomeId = Guid.NewGuid();
    private static readonly Guid ExpenseId = Guid.NewGuid();
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
            new Account { Id = IncomeId, Name = "Raffle Income", Type = AccountType.Income, AccountNumber = "4000", SortOrder = 0, IsSystem = false, CreatedAt = now, UpdatedAt = now },
            new Account { Id = ExpenseId, Name = "Props", Type = AccountType.Expense, AccountNumber = "5000", SortOrder = 0, IsSystem = false, CreatedAt = now, UpdatedAt = now },
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

    // --- FR-009, FR-012, US3-1, US3-5: posting -------------------------------------------

    [Fact]
    public async Task Should_RaiseBankByTotalAndLowerUndepositedFundsAndPettyCash_When_ChequesAndCashDeposited_Integration()
    {
        await ReceiveCashAsync(Day1, 100m);
        await ReceiveChequeAsync(Day1, 50m, "CHQ 1");
        await ReceiveChequeAsync(Day1, 70m, "CHQ 2");
        await ReceiveChequeAsync(Day1, 30m, "CHQ 3");
        var awaiting = (await Banking().GetOverviewAsync(TestContext.Current.CancellationToken)).AwaitingItems;
        var chosen = awaiting.Where(i => i.Reference is "CHQ 1" or "CHQ 2").Select(i => i.Id).ToList();

        await Banking().RecordDepositAsync(
            new RecordDepositRequest { Date = Day2, BankAccountId = BankId, ReceivedItemIds = chosen, CashAmount = 40m },
            TestContext.Current.CancellationToken);

        Assert.Equal(160m, await BalanceAsync(BankId));
        Assert.Equal(30m, await BalanceAsync(SystemAccounts.UndepositedFundsId));
        Assert.Equal(60m, await BalanceAsync(SystemAccounts.CashId));
        var after = await Banking().GetOverviewAsync(TestContext.Current.CancellationToken);
        var left = Assert.Single(after.AwaitingItems);
        Assert.Equal("CHQ 3", left.Reference);
        Assert.DoesNotContain(after.AwaitingItems, i => chosen.Contains(i.Id));
    }

    // --- FR-013, SC-003, US3-2: reconciliation ------------------------------------------

    [Fact]
    public async Task Should_ShowOneReconciliationLineForFullTotal_When_DepositOfTenReceiptsReconciled_Integration()
    {
        for (var i = 1; i <= 10; i++)
            await ReceiveChequeAsync(Day1, 10m, $"CHQ {i}");
        var ids = (await Banking().GetOverviewAsync(TestContext.Current.CancellationToken)).AwaitingItems.Select(i => i.Id).ToList();

        await Banking().RecordDepositAsync(
            new RecordDepositRequest { Date = Day2, BankAccountId = BankId, ReceivedItemIds = ids },
            TestContext.Current.CancellationToken);

        var service = new BankReconciliationService(new BankReconciliationRepository(_db), new AccountRepository(_db), Gl(), Audit(), RealLocalizer.Instance);
        var draft = await service.StartDraftAsync(
            new StartReconciliationRequest { AccountId = BankId, StatementDate = Day3, StatementClosingBalance = 100m },
            TestContext.Current.CancellationToken);
        var workspace = await service.GetWorkspaceAsync(draft.Id, TestContext.Current.CancellationToken);

        var line = Assert.Single(workspace.Transactions);
        Assert.Equal(100m, line.Transaction.DebitAmount);
    }

    // --- FR-010, US3-3, US3-4: refusals --------------------------------------------------

    [Fact]
    public async Task Should_RefuseWithMessage_When_NoChequesAndNoCash_Integration()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => Banking().RecordDepositAsync(
            new RecordDepositRequest { Date = Day1, BankAccountId = BankId }, TestContext.Current.CancellationToken));

        Assert.Equal("A deposit needs at least one cheque or a cash amount.", ex.Message);
        Assert.Equal(0m, await BalanceAsync(BankId));
    }

    [Fact]
    public async Task Should_RefuseTellingCashAvailable_When_CashAboveBalanceOnDepositDate_Integration()
    {
        await ReceiveCashAsync(Day1, 50m);
        await ReceiveCashAsync(Day2, 50m);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Banking().RecordDepositAsync(
            new RecordDepositRequest { Date = Day1, BankAccountId = BankId, CashAmount = 80m }, TestContext.Current.CancellationToken));

        Assert.Contains(MoneyFormatter.Format(50m), ex.Message);
        Assert.Equal(100m, await BalanceAsync(SystemAccounts.CashId));
        Assert.Equal(0m, await BalanceAsync(BankId));
    }

    [Fact]
    public async Task Should_RefuseTellingCashAvailable_When_CashAboveCurrentBalance_Integration()
    {
        await ReceiveCashAsync(Day1, 100m);
        await BuildExpenseService().RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = Day2, Amount = 70m, BankAccountId = SystemAccounts.CashId, ExpenseAccountId = ExpenseId, Payee = "Hardware shop"
        }, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Banking().RecordDepositAsync(
            new RecordDepositRequest { Date = Day1, BankAccountId = BankId, CashAmount = 60m }, TestContext.Current.CancellationToken));

        Assert.Contains(MoneyFormatter.Format(30m), ex.Message);
        Assert.Equal(0m, await BalanceAsync(BankId));
    }

    [Fact]
    public async Task Should_RefuseSecondDeposit_When_ChequeAlreadyDeposited_Integration()
    {
        await ReceiveChequeAsync(Day1, 50m, "CHQ 1");
        var ids = (await Banking().GetOverviewAsync(TestContext.Current.CancellationToken)).AwaitingItems.Select(i => i.Id).ToList();
        await Banking().RecordDepositAsync(
            new RecordDepositRequest { Date = Day2, BankAccountId = BankId, ReceivedItemIds = ids }, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Banking().RecordDepositAsync(
            new RecordDepositRequest { Date = Day2, BankAccountId = BankId, ReceivedItemIds = ids }, TestContext.Current.CancellationToken));

        Assert.Equal("One or more of the selected items is no longer awaiting deposit.", ex.Message);
        Assert.Equal(50m, await BalanceAsync(BankId));
    }

    [Fact]
    public async Task Should_RefuseWithMessage_When_DepositDateBeforeChequeReceived_Integration()
    {
        await ReceiveChequeAsync(Day2, 50m, "CHQ 1");
        var ids = (await Banking().GetOverviewAsync(TestContext.Current.CancellationToken)).AwaitingItems.Select(i => i.Id).ToList();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Banking().RecordDepositAsync(
            new RecordDepositRequest { Date = Day1, BankAccountId = BankId, ReceivedItemIds = ids }, TestContext.Current.CancellationToken));

        Assert.Equal("The deposit date cannot be earlier than the date an included cheque was received.", ex.Message);
        Assert.Equal(0m, await BalanceAsync(BankId));
    }

    [Fact]
    public async Task Should_RefuseAndPostNothing_When_DepositDatedInClosedPeriod_Integration()
    {
        await ReceiveChequeAsync(Day1, 50m, "CHQ 1");
        var ids = (await Banking().GetOverviewAsync(TestContext.Current.CancellationToken)).AwaitingItems.Select(i => i.Id).ToList();
        (await _db.Settings.SingleAsync(TestContext.Current.CancellationToken)).ClosedThroughDate = Day1;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ClosedPeriodException>(() => Banking().RecordDepositAsync(
            new RecordDepositRequest { Date = Day1, BankAccountId = BankId, ReceivedItemIds = ids }, TestContext.Current.CancellationToken));

        Assert.Equal(0m, await BalanceAsync(BankId));
        Assert.Equal(50m, await BalanceAsync(SystemAccounts.UndepositedFundsId));
        Assert.Single((await Banking().GetOverviewAsync(TestContext.Current.CancellationToken)).AwaitingItems);
    }

    // --- SC-002, FR-019: integrity -------------------------------------------------------

    [Fact]
    public async Task Should_KeepTrialBalanceBalancedAndWriteAuditEntry_When_DepositRecorded_Integration()
    {
        await ReceiveCashAsync(Day1, 100m);
        await ReceiveChequeAsync(Day1, 50m, "CHQ 1");
        var ids = (await Banking().GetOverviewAsync(TestContext.Current.CancellationToken)).AwaitingItems.Select(i => i.Id).ToList();

        var deposit = await Banking().RecordDepositAsync(
            new RecordDepositRequest { Date = Day2, BankAccountId = BankId, ReceivedItemIds = ids, CashAmount = 25m },
            TestContext.Current.CancellationToken);

        var transactions = await _db.Transactions.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(transactions.Sum(t => t.DebitAmount), transactions.Sum(t => t.CreditAmount));
        var audit = await _db.AuditTrailEntries.SingleAsync(
            a => a.EntityType == nameof(BankDeposit) && a.EntityId == deposit.Id, TestContext.Current.CancellationToken);
        Assert.Equal(AuditAction.Create, audit.Action);
    }

    // --- Helpers -------------------------------------------------------------------------

    private Task ReceiveCashAsync(DateTime date, decimal amount) =>
        BuildIncomeService().RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = date, Amount = amount, AccountId = IncomeId, PaymentMethod = PaymentMethod.Cash
        }, TestContext.Current.CancellationToken);

    private Task ReceiveChequeAsync(DateTime date, decimal amount, string reference) =>
        BuildIncomeService().RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = date, Amount = amount, AccountId = IncomeId, PaymentMethod = PaymentMethod.Check,
            Reference = reference, Description = "Sponsor " + reference
        }, TestContext.Current.CancellationToken);

    // Debits minus credits on an account.
    private async Task<decimal> BalanceAsync(Guid accountId) =>
        await _db.Transactions.Where(t => t.AccountId == accountId).SumAsync(t => t.DebitAmount - t.CreditAmount, TestContext.Current.CancellationToken);

    private GLRepository Gl() => new(_db, new ClosedPeriodGuard(new SettingsRepository(_db)));

    // A real audit repository so the audit entry is actually written (FR-019).
    private AuditTrailService Audit() =>
        new(new AuditTrailRepository(_db), NullLogger<AuditTrailService>.Instance);

    private IncomeEntryService BuildIncomeService() =>
        new(new AccountRepository(_db), Gl(), new JournalEntryRepository(_db), new SettingsRepository(_db), Audit(), new UnitOfWork(_db), RealLocalizer.Instance, new ExternalOrganisationRepository(_db), new ReceivedItemRepository(_db));

    private ExpensePaymentService BuildExpenseService() =>
        new(new AccountRepository(_db), Gl(), new JournalEntryRepository(_db), new SettingsRepository(_db), Audit(), new UnitOfWork(_db), RealLocalizer.Instance, new ExternalOrganisationRepository(_db));

    private BankingService Banking() =>
        new(new AccountRepository(_db), Gl(), new JournalEntryRepository(_db), new ReceivedItemRepository(_db), new BankDepositRepository(_db),
            new BankReconciliationRepository(_db), new ClosedPeriodGuard(new SettingsRepository(_db)), Audit(), new UnitOfWork(_db), RealLocalizer.Instance);
}
