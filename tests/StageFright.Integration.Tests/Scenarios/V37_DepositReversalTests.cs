using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Organisations;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V37 (spec 033, User Story 5): the Banking history and a deposit's detail, and reversing
/// a deposit, which restores every account, puts its cheques back on the awaiting list and is refused once
/// reversed, in a closed period or when cleared in a finalised reconciliation.
/// Real in-memory SQLite with the full EF migration set and a real audit trail repository.
/// </summary>
public sealed class V37_DepositReversalTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid IncomeId = Guid.NewGuid();
    private static readonly Guid ExpenseId = Guid.NewGuid();
    private static readonly Guid BankId = Guid.NewGuid();
    private static readonly Guid OrganisationId = Guid.NewGuid();
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
            new Account { Id = IncomeId, Name = "Sponsorship", Type = AccountType.Income, AccountNumber = "4000", SortOrder = 0, IsSystem = false, CreatedAt = now, UpdatedAt = now },
            new Account { Id = ExpenseId, Name = "Hall Hire", Type = AccountType.Expense, AccountNumber = "5000", SortOrder = 0, IsSystem = false, CreatedAt = now, UpdatedAt = now },
            new Account { Id = BankId, Name = "Community Bank", Type = AccountType.Asset, AccountNumber = "1110", IsBankAccount = true, SortOrder = 0, IsSystem = false, CreatedAt = now, UpdatedAt = now });
        _db.ExternalOrganisations.Add(new ExternalOrganisation { Id = OrganisationId, Name = "Bowling Club", CreatedAt = now, UpdatedAt = now });
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

    // --- FR-015, US5-1: history ------------------------------------------------------------

    [Fact]
    public async Task Should_ListDateTypeBankTotalAndStatus_When_DepositReversedAndWithdrawalRecorded_Integration()
    {
        var deposit = await DepositChequesAndCashAsync();
        await Banking().RecordWithdrawalAsync(
            new RecordCashWithdrawalRequest { Date = Day3, BankAccountId = BankId, Amount = 20m, Purpose = "Float" },
            TestContext.Current.CancellationToken);
        await Banking().ReverseDepositAsync(deposit.Id, TestContext.Current.CancellationToken);

        var history = await Banking().GetHistoryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, history.Count);
        var withdrawal = history[0];
        Assert.Equal((BankingHistoryEntryType.Withdrawal, Day3, "Community Bank", 20m, false),
            (withdrawal.Type, withdrawal.Date, withdrawal.BankAccountName, withdrawal.Total, withdrawal.IsReversed));
        var reversed = history[1];
        Assert.Equal((deposit.Id, BankingHistoryEntryType.Deposit, Day2, "Community Bank", 160m, true),
            (reversed.Id, reversed.Type, reversed.Date, reversed.BankAccountName, reversed.Total, reversed.IsReversed));
    }

    // --- US5-2: detail ---------------------------------------------------------------------

    [Fact]
    public async Task Should_ShowCashAndEachChequeSummingToTotal_When_DepositOpened_Integration()
    {
        var deposit = await DepositChequesAndCashAsync();

        var detail = await Banking().GetDepositAsync(deposit.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(detail);
        Assert.Equal(40m, detail.CashAmount);
        Assert.Equal(["CHQ 1", "CHQ 2"], detail.Items.Select(i => i.Reference).Order());
        Assert.Equal(160m, detail.Total);
        Assert.Equal(detail.Total, detail.CashAmount + detail.Items.Sum(i => i.Amount));
        Assert.True(detail.CanReverse);
        Assert.False(detail.IsReversed);
    }

    // --- FR-016, SC-005, US5-3: reversal ---------------------------------------------------

    [Fact]
    public async Task Should_RestoreAllThreeBalancesAndReturnCheques_When_DepositReversed_Integration()
    {
        await ReceiveCashAsync(Day1, 100m);
        await ReceiveChequeAsync(Day1, 50m, "CHQ 1");
        await ReceiveChequeAsync(Day1, 70m, "CHQ 2");
        var bankBefore = await BalanceAsync(BankId);
        var undepositedBefore = await BalanceAsync(SystemAccounts.UndepositedFundsId);
        var pettyCashBefore = await BalanceAsync(SystemAccounts.CashId);
        var awaitingBefore = await AwaitingIdsAsync();
        var deposit = await DepositAsync(awaitingBefore, 40m);

        await Banking().ReverseDepositAsync(deposit.Id, TestContext.Current.CancellationToken);

        Assert.Equal(bankBefore, await BalanceAsync(BankId));
        Assert.Equal(undepositedBefore, await BalanceAsync(SystemAccounts.UndepositedFundsId));
        Assert.Equal(pettyCashBefore, await BalanceAsync(SystemAccounts.CashId));
        Assert.Equal(awaitingBefore.Order(), (await AwaitingIdsAsync()).Order());
        var reversal = await _db.JournalEntries.SingleAsync(j => j.Type == JournalEntryType.BankDepositReversal, TestContext.Current.CancellationToken);
        Assert.Equal(Day2, reversal.Date);
        var detail = await Banking().GetDepositAsync(deposit.Id, TestContext.Current.CancellationToken);
        Assert.True(detail!.IsReversed);
        Assert.NotNull(detail.ReversedAt);
        Assert.False(detail.CanReverse);
    }

    // --- US5-4, US5-5, US5-6, FR-018: refusals ---------------------------------------------

    [Fact]
    public async Task Should_RefuseSecondReversal_When_DepositAlreadyReversed_Integration()
    {
        var deposit = await DepositChequesAndCashAsync();
        await Banking().ReverseDepositAsync(deposit.Id, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Banking().ReverseDepositAsync(deposit.Id, TestContext.Current.CancellationToken));

        Assert.Equal("This deposit has already been reversed.", ex.Message);
        Assert.Equal(1, await _db.JournalEntries.CountAsync(j => j.Type == JournalEntryType.BankDepositReversal, TestContext.Current.CancellationToken));
        var detail = await Banking().GetDepositAsync(deposit.Id, TestContext.Current.CancellationToken);
        Assert.Equal("This deposit has already been reversed.", detail!.CannotReverseReason);
    }

    [Fact]
    public async Task Should_RefuseAndPostNothing_When_DepositDatedInClosedPeriod_Integration()
    {
        var deposit = await DepositChequesAndCashAsync();
        (await _db.Settings.SingleAsync(TestContext.Current.CancellationToken)).ClosedThroughDate = Day2;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ClosedPeriodException>(() =>
            Banking().ReverseDepositAsync(deposit.Id, TestContext.Current.CancellationToken));

        Assert.Equal(160m, await BalanceAsync(BankId));
        Assert.Single(await AwaitingIdsAsync());
        var detail = await Banking().GetDepositAsync(deposit.Id, TestContext.Current.CancellationToken);
        Assert.False(detail!.CanReverse);
        Assert.False(detail.IsReversed);
        Assert.Equal(
            "This transaction is dated in a closed financial period and cannot be posted. Change the date to a period that is still open.",
            detail.CannotReverseReason);
    }

    [Fact]
    public async Task Should_RefuseWithReason_When_DepositClearedInFinalisedReconciliation_Integration()
    {
        var deposit = await DepositChequesAndCashAsync();
        var reconciliation = Reconciliation();
        var draftId = await ClearDepositInDraftAsync(reconciliation, 160m);
        await reconciliation.FinaliseAsync(draftId, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Banking().ReverseDepositAsync(deposit.Id, TestContext.Current.CancellationToken));

        const string message = "This deposit is part of a finalised bank reconciliation and cannot be reversed.";
        Assert.Equal(message, ex.Message);
        Assert.Equal(160m, await BalanceAsync(BankId));
        var detail = await Banking().GetDepositAsync(deposit.Id, TestContext.Current.CancellationToken);
        Assert.False(detail!.CanReverse);
        Assert.Equal(message, detail.CannotReverseReason);
    }

    // --- US5-7 and the negative-balance edge case: allowed reversals -------------------------

    [Fact]
    public async Task Should_ReverseAndUnclearDraftLine_When_DepositClearedOnlyInDraftReconciliation_Integration()
    {
        var deposit = await DepositChequesAndCashAsync();
        var reconciliation = Reconciliation();
        var draftId = await ClearDepositInDraftAsync(reconciliation, 160m);

        await Banking().ReverseDepositAsync(deposit.Id, TestContext.Current.CancellationToken);

        Assert.Equal(0m, await BalanceAsync(BankId));
        var workspace = await reconciliation.GetWorkspaceAsync(draftId, TestContext.Current.CancellationToken);
        Assert.Equal(0m, workspace.ClearedTotal);
        Assert.Empty(workspace.Reconciliation.Lines);
    }

    [Fact]
    public async Task Should_StillReverse_When_DepositedMoneyAlreadySpentFromBank_Integration()
    {
        await ReceiveCashAsync(Day1, 100m);
        var deposit = await DepositAsync([], 100m);
        await BuildExpenseService().RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = Day3, Amount = 80m, BankAccountId = BankId, ExpenseAccountId = ExpenseId, Payee = "Town Hall"
        }, TestContext.Current.CancellationToken);

        await Banking().ReverseDepositAsync(deposit.Id, TestContext.Current.CancellationToken);

        Assert.Equal(-80m, await BalanceAsync(BankId));
        Assert.Equal(100m, await BalanceAsync(SystemAccounts.CashId));
        Assert.Equal(100m, (await Banking().GetOverviewAsync(TestContext.Current.CancellationToken)).PettyCashBalance);
    }

    // --- FR-025, SC-002, FR-019: integrity -------------------------------------------------

    [Fact]
    public async Task Should_LeaveOrganisationTotalsUnchanged_When_ChequeDepositedAndReversed_Integration()
    {
        await BuildIncomeService().RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = Day1, Amount = 250m, AccountId = IncomeId, PaymentMethod = PaymentMethod.Check,
            Reference = "CHQ 9", Description = "Sponsorship", ExternalOrganisationId = OrganisationId
        }, TestContext.Current.CancellationToken);
        var before = await OrganisationHistory().GetHistoryAsync(OrganisationId, TestContext.Current.CancellationToken);

        var deposit = await DepositAsync(await AwaitingIdsAsync(), 0m);
        var afterDeposit = await OrganisationHistory().GetHistoryAsync(OrganisationId, TestContext.Current.CancellationToken);
        await Banking().ReverseDepositAsync(deposit.Id, TestContext.Current.CancellationToken);
        var afterReversal = await OrganisationHistory().GetHistoryAsync(OrganisationId, TestContext.Current.CancellationToken);

        Assert.Equal(250m, before.TotalReceived);
        Assert.Equal((before.TotalReceived, before.TotalPaid, before.Entries.Count),
            (afterDeposit.TotalReceived, afterDeposit.TotalPaid, afterDeposit.Entries.Count));
        Assert.Equal((before.TotalReceived, before.TotalPaid, before.Entries.Count),
            (afterReversal.TotalReceived, afterReversal.TotalPaid, afterReversal.Entries.Count));
        Assert.False(await _db.JournalEntries.AnyAsync(
            j => (j.Type == JournalEntryType.BankDeposit || j.Type == JournalEntryType.BankDepositReversal) && j.ExternalOrganisationId != null,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_KeepTrialBalanceBalancedAndWriteAuditEntry_When_DepositReversed_Integration()
    {
        var deposit = await DepositChequesAndCashAsync();

        await Banking().ReverseDepositAsync(deposit.Id, TestContext.Current.CancellationToken);

        var transactions = await _db.Transactions.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(transactions.Sum(t => t.DebitAmount), transactions.Sum(t => t.CreditAmount));
        var audit = await _db.AuditTrailEntries.SingleAsync(
            a => a.EntityType == nameof(BankDeposit) && a.EntityId == deposit.Id && a.Action == AuditAction.Update,
            TestContext.Current.CancellationToken);
        Assert.Contains("Reversed", audit.NewValue);
    }

    // --- Helpers -----------------------------------------------------------------------------

    // Cash 100, cheques 50, 70 and 30; deposits cheques 1 and 2 plus 40 cash on Day2, a 160 total.
    private async Task<BankDeposit> DepositChequesAndCashAsync()
    {
        await ReceiveCashAsync(Day1, 100m);
        await ReceiveChequeAsync(Day1, 50m, "CHQ 1");
        await ReceiveChequeAsync(Day1, 70m, "CHQ 2");
        await ReceiveChequeAsync(Day1, 30m, "CHQ 3");
        var chosen = (await Banking().GetOverviewAsync(TestContext.Current.CancellationToken)).AwaitingItems
            .Where(i => i.Reference is "CHQ 1" or "CHQ 2").Select(i => i.Id).ToList();
        return await DepositAsync(chosen, 40m);
    }

    private Task<BankDeposit> DepositAsync(IReadOnlyList<Guid> itemIds, decimal cash) =>
        Banking().RecordDepositAsync(
            new RecordDepositRequest { Date = Day2, BankAccountId = BankId, ReceivedItemIds = itemIds.ToList(), CashAmount = cash },
            TestContext.Current.CancellationToken);

    // Starts a draft reconciliation to Day3 and clears the deposit's bank line in it.
    private static async Task<Guid> ClearDepositInDraftAsync(BankReconciliationService reconciliation, decimal closingBalance)
    {
        var draft = await reconciliation.StartDraftAsync(
            new StartReconciliationRequest { AccountId = BankId, StatementDate = Day3, StatementClosingBalance = closingBalance },
            TestContext.Current.CancellationToken);
        var workspace = await reconciliation.GetWorkspaceAsync(draft.Id, TestContext.Current.CancellationToken);
        var line = Assert.Single(workspace.Transactions);
        await reconciliation.ToggleClearAsync(draft.Id, line.Transaction.Id, TestContext.Current.CancellationToken);
        return draft.Id;
    }

    private async Task<List<Guid>> AwaitingIdsAsync() =>
        (await Banking().GetOverviewAsync(TestContext.Current.CancellationToken)).AwaitingItems.Select(i => i.Id).ToList();

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

    private BankReconciliationService Reconciliation() =>
        new(new BankReconciliationRepository(_db), new AccountRepository(_db), Gl(), Audit(), RealLocalizer.Instance);

    private OrganisationHistoryService OrganisationHistory() => new(new JournalEntryRepository(_db));

    private BankingService Banking() =>
        new(new AccountRepository(_db), Gl(), new JournalEntryRepository(_db), new ReceivedItemRepository(_db), new BankDepositRepository(_db),
            new BankReconciliationRepository(_db), new ClosedPeriodGuard(new SettingsRepository(_db)), Audit(), new UnitOfWork(_db), RealLocalizer.Instance);
}
