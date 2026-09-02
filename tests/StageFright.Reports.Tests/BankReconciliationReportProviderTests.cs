using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for BankReconciliationReportProvider — the conventional adjusted-balance layout
/// (spec 028 US5, FR-013…FR-015):
/// - Metadata (id, module, order) and the empty state
/// - One section per bank account, headed with the account / statement line
/// - "Balance per bank statement" and "Balance per general ledger" both shown (FR-013)
/// - Outstanding deposits / payments listed AND carried into the adjusted-bank-balance
///   arithmetic — not merely listed (FR-014)
/// - Adjusted bank balance equals the ledger balance; the reconciled residual is zero (SC-008)
/// - Runs with and without outstanding items
/// - Account filter narrows to one account
/// - Money cells route through MoneyFormatter (configured symbol + precision, FR-003)
/// </summary>
public class BankReconciliationReportProviderTests
{
    private readonly IBankReconciliationRepository _recRepo = Substitute.For<IBankReconciliationRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IGLRepository _gl = Substitute.For<IGLRepository>();
    private readonly BankReconciliationReportProvider _sut;

    private static readonly Guid CashId = Guid.NewGuid();
    private static readonly Guid SavingsId = Guid.NewGuid();
    private static readonly DateTime StatementDate = new(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

    public BankReconciliationReportProviderTests()
    {
        _accounts.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Account>>(new List<Account>
            {
                MakeBankAccount(CashId, "Cash on Hand", "1100"),
                MakeBankAccount(SavingsId, "Savings", "1110")
            }));

        _recRepo.GetByAccountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<BankReconciliation>>(new List<BankReconciliation>()));

        _gl.GetUnreconciledByAccountAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Transaction>>(new List<Transaction>()));
        _gl.GetAccountBalanceAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0m));

        _sut = new BankReconciliationReportProvider(_recRepo, _accounts, _gl, RealLocalizer.Instance);
    }

    // --- Metadata ---

    [Fact]
    public void ReportId_IsBankReconciliation() => Assert.Equal("bank-reconciliation", _sut.ReportId);

    [Fact]
    public void ModuleName_IsFinance_And_DisplayOrderIs50()
    {
        Assert.Equal("Finance", _sut.ModuleName);
        Assert.Equal(50, _sut.DisplayOrder);
    }

    // --- Empty state ---

    [Fact]
    public async Task GenerateAsync_NoReconciliations_ProducesEmptyReportWithExplanation()
    {
        var result = await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        Assert.Empty(result.Sections);
        Assert.Contains("No reconciliations", result.SubTitle);
    }

    // --- Conventional adjusted-balance layout ---

    [Fact]
    public async Task GenerateAsync_LatestReconciliation_ProducesOneSectionPerAccount()
    {
        SetupReconciliation(CashId, closing: 150m,
            cleared: [MakeTransaction(CashId, debit: 200m), MakeTransaction(CashId, credit: 50m)],
            ledgerBalance: 150m);

        var result = await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        var section = Assert.Single(result.Sections);
        Assert.Contains("Cash on Hand", section.Heading);
        Assert.Contains("1100", section.Heading);
    }

    [Fact]
    public async Task GenerateAsync_ShowsBothBankStatementBalance_AndGeneralLedgerBalance()
    {
        SetupReconciliation(CashId, closing: 150m,
            cleared: [MakeTransaction(CashId, debit: 150m)], ledgerBalance: 150m);

        var section = (await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken)).Sections[0];

        Assert.Equal(MoneyFormatter.Format(150m), AmountFor(section, "Reports_BankReconciliation_BalancePerBankStatement"));
        Assert.Equal(MoneyFormatter.Format(150m), AmountFor(section, "Reports_BankReconciliation_BalancePerGeneralLedger"));
    }

    [Fact]
    public async Task GenerateAsync_OutstandingItems_ListedAndCarriedIntoAdjustedBankBalance()
    {
        // Statement 150; outstanding deposit 40, outstanding payment 25 → adjusted 165 == ledger 165.
        SetupReconciliation(CashId, closing: 150m,
            cleared: [MakeTransaction(CashId, debit: 150m)],
            ledgerBalance: 165m,
            outstanding:
            [
                MakeTransaction(CashId, debit: 40m, description: "Deposit in transit"),
                MakeTransaction(CashId, credit: 25m, description: "Unpresented cheque")
            ]);

        var section = (await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken)).Sections[0];

        // each outstanding item is listed
        Assert.Contains(section.Rows, r => r.Cells[1] == "Deposit in transit" && r.Cells[2] == MoneyFormatter.Format(40m));
        Assert.Contains(section.Rows, r => r.Cells[1] == "Unpresented cheque" && r.Cells[3] == MoneyFormatter.Format(25m));

        // the totals are carried into the arithmetic, not merely listed
        Assert.Equal(MoneyFormatter.Format(40m), DepositFor(section, "Reports_BankReconciliation_AddOutstandingDeposits"));
        Assert.Equal(MoneyFormatter.Format(25m), AmountFor(section, "Reports_BankReconciliation_LessOutstandingPayments"));
        Assert.Equal(MoneyFormatter.Format(165m), AmountFor(section, "Reports_BankReconciliation_AdjustedBankBalance"));
    }

    [Fact]
    public async Task GenerateAsync_WhenReconciled_AdjustedBalanceEqualsLedger_AndResidualIsZero()
    {
        SetupReconciliation(CashId, closing: 150m,
            cleared: [MakeTransaction(CashId, debit: 150m)],
            ledgerBalance: 165m,
            outstanding: [MakeTransaction(CashId, debit: 40m), MakeTransaction(CashId, credit: 25m)]);

        var section = (await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken)).Sections[0];

        Assert.Equal(
            AmountFor(section, "Reports_BankReconciliation_AdjustedBankBalance"),
            AmountFor(section, "Reports_BankReconciliation_BalancePerGeneralLedger"));
        Assert.Equal(MoneyFormatter.Format(0m), AmountFor(section, "Reports_BankReconciliation_Reconciled"));
        Assert.True(RowFor(section, "Reports_BankReconciliation_Reconciled").IsEmphasized);
    }

    [Fact]
    public async Task GenerateAsync_WithNoOutstandingItems_StillShowsBothBalances_AndProvesAgreement()
    {
        SetupReconciliation(CashId, closing: 150m,
            cleared: [MakeTransaction(CashId, debit: 150m)], ledgerBalance: 150m);

        var section = (await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken)).Sections[0];

        Assert.Equal(MoneyFormatter.Format(150m), AmountFor(section, "Reports_BankReconciliation_BalancePerBankStatement"));
        Assert.Equal(MoneyFormatter.Format(150m), AmountFor(section, "Reports_BankReconciliation_BalancePerGeneralLedger"));
        Assert.Equal(MoneyFormatter.Format(150m), AmountFor(section, "Reports_BankReconciliation_AdjustedBankBalance"));
        Assert.Equal(MoneyFormatter.Format(0m), DepositFor(section, "Reports_BankReconciliation_AddOutstandingDeposits"));
        Assert.Equal(MoneyFormatter.Format(0m), AmountFor(section, "Reports_BankReconciliation_Reconciled"));
    }

    // --- Account filter ---

    [Fact]
    public async Task GenerateAsync_AccountFilterByNumber_NarrowsToOneAccount()
    {
        SetupReconciliation(CashId, closing: 0m, cleared: [], ledgerBalance: 0m);
        SetupReconciliation(SavingsId, closing: 0m, cleared: [], ledgerBalance: 0m);

        var filters = new ReportFilterValues();
        filters.Set("account", "1110");
        var result = await _sut.GenerateAsync(filters, TestContext.Current.CancellationToken);

        var section = Assert.Single(result.Sections);
        Assert.Contains("Savings", section.Heading);
    }

    [Fact]
    public async Task GenerateAsync_AccountFilterByName_MatchesCaseInsensitively()
    {
        SetupReconciliation(CashId, closing: 0m, cleared: [], ledgerBalance: 0m);
        SetupReconciliation(SavingsId, closing: 0m, cleared: [], ledgerBalance: 0m);

        var filters = new ReportFilterValues();
        filters.Set("account", "cash");
        var result = await _sut.GenerateAsync(filters, TestContext.Current.CancellationToken);

        var section = Assert.Single(result.Sections);
        Assert.Contains("Cash on Hand", section.Heading);
    }

    // --- Helpers ---

    private static string Label(string key) => RealLocalizer.Instance.Get<ReportsResource>(key);

    private static ReportRow RowFor(ReportSection section, string labelKey) =>
        section.Rows.Single(r => r.Cells.Count > 1 && r.Cells[1] == Label(labelKey));

    // Balance lines carry their amount in the last (Payment) column, matching the report's
    // label/amount-row convention; the outstanding-deposit subtotal carries it in the Deposit column.
    private static string AmountFor(ReportSection section, string labelKey) => RowFor(section, labelKey).Cells[3];

    private static string DepositFor(ReportSection section, string labelKey) => RowFor(section, labelKey).Cells[2];

    private void SetupReconciliation(Guid accountId, decimal closing, Transaction[] cleared,
        decimal ledgerBalance, Transaction[]? outstanding = null)
    {
        var rec = new BankReconciliation
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            StatementDate = StatementDate,
            OpeningBalance = 0m,
            StatementClosingBalance = closing,
            Status = ReconciliationStatus.Finalised,
            Lines = cleared.Select(t => new ReconciliationLine
            {
                Id = Guid.NewGuid(), ReconciliationId = accountId,
                TransactionId = t.Id, Transaction = t, CreatedAt = DateTime.UtcNow
            }).ToList()
        };

        _recRepo.GetByAccountAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<BankReconciliation>>(new List<BankReconciliation> { rec }));
        _recRepo.GetByIdAsync(rec.Id, Arg.Any<CancellationToken>()).Returns(rec);
        _gl.GetAccountBalanceAsync(accountId, StatementDate, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ledgerBalance));
        _gl.GetUnreconciledByAccountAsync(accountId, StatementDate, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Transaction>>(new List<Transaction>(outstanding ?? [])));
    }

    private static Account MakeBankAccount(Guid id, string name, string number) => new()
    {
        Id = id, Name = name, Type = AccountType.Asset, AccountNumber = number,
        IsBankAccount = true, CreatedAt = DateTime.UtcNow
    };

    private static Transaction MakeTransaction(Guid accountId, decimal debit = 0m, decimal credit = 0m,
        string? description = null) => new()
    {
        Id = Guid.NewGuid(), AccountId = accountId, Date = StatementDate.AddDays(-7),
        DebitAmount = debit, CreditAmount = credit, GLAccount = "1100",
        Description = description ?? (debit != 0m ? "Deposit" : "Payment"), CreatedAt = DateTime.UtcNow
    };
}
