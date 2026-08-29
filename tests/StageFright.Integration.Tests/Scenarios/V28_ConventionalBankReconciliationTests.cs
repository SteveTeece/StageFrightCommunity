using Microsoft.EntityFrameworkCore;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;
using StageFright.Reports.Resources;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// V28 acceptance — US5 AC-1…AC-3 (spec 028): the bank reconciliation report follows the
/// conventional adjusted-balance layout. For a finalised reconciliation with known outstanding
/// deposits + payments, and for one with none, it shows "balance per bank statement" and
/// "balance per general ledger", carries every adjusting item into the arithmetic (not merely
/// lists it), and demonstrates the two sides agree. A finalised reconciliation is unchanged and
/// non-editable on later view, and finalisation still required it to balance.
/// Real in-memory SQLite + full migrations.
/// </summary>
[Collection("MoneyFormatterState")]
public sealed class V28_ConventionalBankReconciliationTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid BankId = Guid.NewGuid();
    private static readonly Guid PettyId = Guid.NewGuid();
    private static readonly Guid SaverId = Guid.NewGuid();

    private static readonly Guid ClearedDepositTxnId = Guid.NewGuid();
    private static readonly Guid ClearedPaymentTxnId = Guid.NewGuid();
    private static readonly Guid OutstandingDepositTxnId = Guid.NewGuid();
    private static readonly Guid OutstandingPaymentTxnId = Guid.NewGuid();
    private static readonly Guid PettyFloatTxnId = Guid.NewGuid();
    private static readonly Guid MainReconciliationId = Guid.NewGuid();

    private static readonly DateTime StatementDate = new(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        // Distinct from the migration-seeded system accounts (1100 Cash, 1200 Member Receivable, …).
        _db.Accounts.AddRange(
            Bank(BankId, "Operating Account", "1150"),
            Bank(PettyId, "Petty Cash", "1250"),
            Bank(SaverId, "Club Saver", "1350"));

        // Only the bank leg of each pair matters to this report (it reads bank-account rows plus
        // GetAccountBalanceAsync for the same account), so the contra legs are omitted for brevity.
        _db.Transactions.AddRange(
            Txn(ClearedDepositTxnId, BankId, StatementDate.AddDays(-10), "Deposits banked", debit: 300m),
            Txn(ClearedPaymentTxnId, BankId, StatementDate.AddDays(-8), "Cheque 2041", credit: 100m),
            Txn(OutstandingDepositTxnId, BankId, StatementDate.AddDays(-2), "Deposit in transit", debit: 40m),
            Txn(OutstandingPaymentTxnId, BankId, StatementDate.AddDays(-1), "Unpresented cheque 2043", credit: 25m),
            Txn(PettyFloatTxnId, PettyId, StatementDate.AddDays(-5), "Float top-up", debit: 60m));

        _db.BankReconciliations.AddRange(
            Finalised(MainReconciliationId, BankId, closing: 200m, ClearedDepositTxnId, ClearedPaymentTxnId),
            Finalised(Guid.NewGuid(), PettyId, closing: 60m, PettyFloatTxnId));

        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Reconciled Players",
            AnnualFee = 50m, AttendanceFee = 10m, MembershipRenewalMonth = 1,
            MaxAgeRangeYears = 150, MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            FinancialYearStartMonth = 7, FinancialYearStartDay = 1,
            CurrencyCode = "AUD", IsTaxApplicable = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        MoneyFormatter.Configure(CurrencyCatalog.Get("AUD"));
    }

    public async ValueTask DisposeAsync()
    {
        MoneyFormatter.Configure(CurrencyCatalog.Default);
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // AC-1: both "balance per bank statement" and "balance per general ledger" appear.
    [Fact]
    public async Task Report_ShowsBalancePerBankStatement_AndBalancePerGeneralLedger()
    {
        var section = await GenerateSectionAsync("1150");

        Assert.Equal(MoneyFormatter.Format(200m), AmountFor(section, "Reports_BankReconciliation_BalancePerBankStatement"));
        Assert.Equal(MoneyFormatter.Format(215m), AmountFor(section, "Reports_BankReconciliation_BalancePerGeneralLedger"));
    }

    // AC-2: each outstanding item is listed AND carried into the adjusted-bank-balance arithmetic.
    [Fact]
    public async Task OutstandingItems_AreListed_AndCarriedIntoTheAdjustedBankBalance()
    {
        var section = await GenerateSectionAsync("1150");

        Assert.Contains(section.Rows, r => r.Cells[1] == "Deposit in transit" && r.Cells[2] == MoneyFormatter.Format(40m));
        Assert.Contains(section.Rows, r => r.Cells[1] == "Unpresented cheque 2043" && r.Cells[3] == MoneyFormatter.Format(25m));

        Assert.Equal(MoneyFormatter.Format(40m), DepositFor(section, "Reports_BankReconciliation_AddOutstandingDeposits"));
        Assert.Equal(MoneyFormatter.Format(25m), AmountFor(section, "Reports_BankReconciliation_LessOutstandingPayments"));
        // 200 (statement) + 40 (deposits) − 25 (payments) = 215.
        Assert.Equal(MoneyFormatter.Format(215m), AmountFor(section, "Reports_BankReconciliation_AdjustedBankBalance"));
    }

    // AC-1: the adjusted bank balance equals the general-ledger balance — the two sides agree.
    [Fact]
    public async Task AdjustedBankBalance_EqualsBalancePerGeneralLedger_TheTwoSidesAgree()
    {
        var section = await GenerateSectionAsync("1150");

        Assert.Equal(
            AmountFor(section, "Reports_BankReconciliation_AdjustedBankBalance"),
            AmountFor(section, "Reports_BankReconciliation_BalancePerGeneralLedger"));
        Assert.Equal(MoneyFormatter.Format(0m), AmountFor(section, "Reports_BankReconciliation_Reconciled"));
    }

    // AC-1 edge case: with no outstanding items the report still shows both balances and proves agreement.
    [Fact]
    public async Task WithNoOutstandingItems_TheReportStillShowsBothBalances_AndProvesAgreement()
    {
        var section = await GenerateSectionAsync("1250");

        Assert.Equal(MoneyFormatter.Format(60m), AmountFor(section, "Reports_BankReconciliation_BalancePerBankStatement"));
        Assert.Equal(MoneyFormatter.Format(60m), AmountFor(section, "Reports_BankReconciliation_BalancePerGeneralLedger"));
        Assert.Equal(MoneyFormatter.Format(60m), AmountFor(section, "Reports_BankReconciliation_AdjustedBankBalance"));
        Assert.Equal(MoneyFormatter.Format(0m), DepositFor(section, "Reports_BankReconciliation_AddOutstandingDeposits"));
        Assert.Equal(MoneyFormatter.Format(0m), AmountFor(section, "Reports_BankReconciliation_Reconciled"));
    }

    // AC-3: a finalised reconciliation cannot be edited on later view, and finalisation required balancing.
    [Fact]
    public async Task FinalisedReconciliation_IsImmutable_AndFinalisationRequiredItToBalance()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new BankReconciliationRepository(_db);

        // Immutable: a finalised reconciliation rejects any line change and is unchanged afterwards.
        await Assert.ThrowsAsync<ReconciliationException>(
            () => repo.AddLineAsync(MainReconciliationId, OutstandingDepositTxnId, ct));

        var reloaded = await repo.GetByIdAsync(MainReconciliationId, ct);
        Assert.NotNull(reloaded);
        Assert.Equal(ReconciliationStatus.Finalised, reloaded!.Status);
        Assert.Equal(200m, reloaded.StatementClosingBalance);
        Assert.Equal(2, reloaded.Lines.Count);

        // Finalisation still requires the reconciliation to balance.
        var service = new BankReconciliationService(
            repo, new AccountRepository(_db), new GLRepository(_db),
            Substitute.For<IAuditTrailService>(), RealLocalizer.Instance);

        var draft = await service.StartDraftAsync(new StartReconciliationRequest
        {
            AccountId = SaverId,
            StatementDate = StatementDate,
            StatementClosingBalance = 999m
        }, ct);

        await Assert.ThrowsAsync<ReconciliationException>(() => service.FinaliseAsync(draft.Id, ct));

        var stillDraft = await repo.GetByIdAsync(draft.Id, ct);
        Assert.Equal(ReconciliationStatus.Draft, stillDraft!.Status);
    }

    // --- Helpers ---

    private BankReconciliationReportProvider Provider() => new(
        new BankReconciliationRepository(_db), new AccountRepository(_db), new GLRepository(_db), RealLocalizer.Instance);

    private async Task<ReportSection> GenerateSectionAsync(string accountFilter)
    {
        var filters = new ReportFilterValues();
        filters.Set("account", accountFilter);
        var report = await Provider().GenerateAsync(filters, TestContext.Current.CancellationToken);
        return report.Sections.Single();
    }

    private static string Label(string key) => RealLocalizer.Instance.Get<ReportsResource>(key);

    private static ReportRow RowFor(ReportSection section, string labelKey) =>
        section.Rows.Single(r => r.Cells.Count > 1 && r.Cells[1] == Label(labelKey));

    private static string AmountFor(ReportSection section, string labelKey) => RowFor(section, labelKey).Cells[3];

    private static string DepositFor(ReportSection section, string labelKey) => RowFor(section, labelKey).Cells[2];

    private static Account Bank(Guid id, string name, string number) => new()
    {
        Id = id, Name = name, Type = AccountType.Asset, AccountNumber = number,
        IsBankAccount = true, SortOrder = 0, IsSystem = false,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Transaction Txn(Guid id, Guid accountId, DateTime date, string description,
        decimal debit = 0m, decimal credit = 0m) => new()
    {
        Id = id, AccountId = accountId, Date = date, GLAccount = "1100",
        DebitAmount = debit, CreditAmount = credit, Description = description, CreatedAt = DateTime.UtcNow
    };

    private static BankReconciliation Finalised(Guid id, Guid accountId, decimal closing, params Guid[] clearedTxnIds)
    {
        var now = DateTime.UtcNow;
        return new BankReconciliation
        {
            Id = id, AccountId = accountId, StatementDate = StatementDate,
            OpeningBalance = 0m, StatementClosingBalance = closing,
            Status = ReconciliationStatus.Finalised, FinalisedAt = now,
            CreatedAt = now, UpdatedAt = now,
            Lines = clearedTxnIds.Select(txnId => new ReconciliationLine
            {
                Id = Guid.NewGuid(), ReconciliationId = id, TransactionId = txnId, CreatedAt = now
            }).ToList()
        };
    }
}
