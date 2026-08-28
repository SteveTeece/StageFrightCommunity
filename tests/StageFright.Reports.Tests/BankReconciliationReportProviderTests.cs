using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for BankReconciliationReportProvider:
/// - Metadata (id, module, order)
/// - Empty state when no reconciliations exist
/// - Cleared deposits / cleared payments / unpresented / summary sections per account
/// - Difference figure in the summary section
/// - Account filter narrows to one account
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

        _sut = new BankReconciliationReportProvider(_recRepo, _accounts, _gl, RealLocalizer.Instance);
    }

    // --- Metadata ---

    [Fact]
    public void ReportId_IsBankReconciliation()
    {
        Assert.Equal("bank-reconciliation", _sut.ReportId);
    }

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

    // --- Sections ---

    [Fact]
    public async Task GenerateAsync_LatestReconciliation_ProducesFourSectionsPerAccount()
    {
        SetupReconciliation(CashId, opening: 100m, closing: 250m,
            cleared: [MakeTransaction(CashId, debit: 200m), MakeTransaction(CashId, credit: 50m)]);

        var result = await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        Assert.Equal(4, result.Sections.Count);
        Assert.Contains("Cleared Deposits", result.Sections[0].Heading);
        Assert.Contains("Cleared Payments", result.Sections[1].Heading);
        Assert.Contains("Unpresented Items", result.Sections[2].Heading);
        Assert.Contains("Summary", result.Sections[3].Heading);
    }

    [Fact]
    public async Task GenerateAsync_ClearedTransactions_SplitIntoDepositsAndPayments()
    {
        SetupReconciliation(CashId, opening: 0m, closing: 150m,
            cleared: [MakeTransaction(CashId, debit: 200m), MakeTransaction(CashId, credit: 50m)]);

        var result = await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        var deposits = result.Sections[0];
        var payments = result.Sections[1];
        Assert.Single(deposits.Rows);
        Assert.Equal("200.00", deposits.Subtotal!.Cells[2]);
        Assert.Single(payments.Rows);
        Assert.Equal("50.00", payments.Subtotal!.Cells[3]);
    }

    [Fact]
    public async Task GenerateAsync_Summary_ContainsZeroDifference_WhenBalanced()
    {
        // Opening 100 + cleared (200 − 50) = 250 = closing → difference 0.00.
        SetupReconciliation(CashId, opening: 100m, closing: 250m,
            cleared: [MakeTransaction(CashId, debit: 200m), MakeTransaction(CashId, credit: 50m)]);

        var result = await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        var summary = result.Sections[3];
        var differenceRow = summary.Rows.Single(r => r.Cells[1] == "Difference");
        Assert.Equal("0.00", differenceRow.Cells[3]);
    }

    [Fact]
    public async Task GenerateAsync_UnpresentedItems_ListedUpToStatementDate()
    {
        SetupReconciliation(CashId, opening: 0m, closing: 0m, cleared: []);
        _gl.GetUnreconciledByAccountAsync(CashId, StatementDate, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Transaction>>(
                new List<Transaction> { MakeTransaction(CashId, debit: 75m) }));

        var result = await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        var unpresented = result.Sections[2];
        Assert.Single(unpresented.Rows);
        Assert.Equal("75.00", unpresented.Rows[0].Cells[2]);
    }

    // --- Account filter ---

    [Fact]
    public async Task GenerateAsync_AccountFilterByNumber_NarrowsToOneAccount()
    {
        SetupReconciliation(CashId, opening: 0m, closing: 0m, cleared: []);
        SetupReconciliation(SavingsId, opening: 0m, closing: 0m, cleared: []);

        var filters = new ReportFilterValues();
        filters.Set("account", "1110");
        var result = await _sut.GenerateAsync(filters, TestContext.Current.CancellationToken);

        Assert.Equal(4, result.Sections.Count);
        Assert.All(result.Sections, s => Assert.Contains("Savings", s.Heading));
    }

    [Fact]
    public async Task GenerateAsync_AccountFilterByName_MatchesCaseInsensitively()
    {
        SetupReconciliation(CashId, opening: 0m, closing: 0m, cleared: []);
        SetupReconciliation(SavingsId, opening: 0m, closing: 0m, cleared: []);

        var filters = new ReportFilterValues();
        filters.Set("account", "cash");
        var result = await _sut.GenerateAsync(filters, TestContext.Current.CancellationToken);

        Assert.All(result.Sections, s => Assert.Contains("Cash on Hand", s.Heading));
    }

    // --- Helpers ---

    private void SetupReconciliation(Guid accountId, decimal opening, decimal closing, Transaction[] cleared)
    {
        var rec = new BankReconciliation
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            StatementDate = StatementDate,
            OpeningBalance = opening,
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
    }

    private static Account MakeBankAccount(Guid id, string name, string number) => new()
    {
        Id = id, Name = name, Type = AccountType.Asset, AccountNumber = number,
        IsBankAccount = true, CreatedAt = DateTime.UtcNow
    };

    private static Transaction MakeTransaction(Guid accountId, decimal debit = 0m, decimal credit = 0m) => new()
    {
        Id = Guid.NewGuid(), AccountId = accountId, Date = StatementDate.AddDays(-7),
        DebitAmount = debit, CreditAmount = credit, GLAccount = "1100",
        Description = debit != 0m ? "Deposit" : "Payment", CreatedAt = DateTime.UtcNow
    };
}
