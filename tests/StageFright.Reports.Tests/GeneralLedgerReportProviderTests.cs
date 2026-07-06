using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for GeneralLedgerReportProvider:
/// - One section per account with opening balance, dated lines, running balance, closing balance
/// - Account filter (number or name, blank = all)
/// - Accounts with no opening balance and no movement omitted
/// - Date-range filter applied
/// </summary>
public class GeneralLedgerReportProviderTests
{
    private readonly IGLRepository _gl = Substitute.For<IGLRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly GeneralLedgerReportProvider _sut;

    public GeneralLedgerReportProviderTests()
    {
        _sut = new GeneralLedgerReportProvider(_gl, _accounts);
        SetupAccounts();
        SetupTransactions();
    }

    [Fact]
    public void ReportId_IsGeneralLedger()
    {
        Assert.Equal("general-ledger", _sut.ReportId);
    }

    [Fact]
    public void ModuleName_IsFinance()
    {
        Assert.Equal("Finance", _sut.ModuleName);
    }

    [Fact]
    public async Task GenerateAsync_AccountWithMovement_ShowsOpeningLinesAndClosing()
    {
        var accountId = Guid.NewGuid();
        SetupAccounts(MakeAccount(accountId, "Cash on Hand", AccountType.Asset, "1100"));
        SetupOpeningBalance(accountId, 100m);
        SetupTransactions(
            MakeTransaction(accountId, "Deposit", 50m, 0m, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)),
            MakeTransaction(accountId, "Withdrawal", 0m, 20m, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        var section = result.Sections.First(s => s.Heading.Contains("Cash on Hand"));
        Assert.Equal("Opening Balance", section.Rows[0].Cells[1]);
        Assert.Equal("100.00", section.Rows[0].Cells[4]);
        Assert.Equal("150.00", section.Rows[1].Cells[4]); // 100 + 50
        Assert.Equal("130.00", section.Rows[2].Cells[4]); // 150 - 20
        Assert.Equal("Closing Balance", section.Subtotal!.Cells[1]);
        Assert.Equal("130.00", section.Subtotal.Cells[4]);
    }

    [Fact]
    public async Task GenerateAsync_AccountWithNoOpeningAndNoMovement_Omitted()
    {
        var accountId = Guid.NewGuid();
        SetupAccounts(MakeAccount(accountId, "Unused Expense", AccountType.Expense, "6100"));
        SetupOpeningBalance(accountId, 0m);
        SetupTransactions();

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.DoesNotContain(result.Sections, s => s.Heading.Contains("Unused Expense"));
    }

    [Fact]
    public async Task GenerateAsync_AccountFilter_MatchesByNumberOrName()
    {
        var matchId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        SetupAccounts(
            MakeAccount(matchId, "Cash on Hand", AccountType.Asset, "1100"),
            MakeAccount(otherId, "Hall Hire", AccountType.Expense, "6000"));
        SetupOpeningBalance(matchId, 10m);
        SetupOpeningBalance(otherId, 10m);

        var filters = CurrentYearFilters();
        filters.Set("account", "1100");

        var result = await _sut.GenerateAsync(filters);

        Assert.Single(result.Sections);
        Assert.Contains("Cash on Hand", result.Sections[0].Heading);
    }

    [Fact]
    public async Task GenerateAsync_DateRangeFilter_Applied()
    {
        var filters = new ReportFilterValues();
        filters.Set("dateFrom", "2026-03-01");
        filters.Set("dateTo", "2026-09-30");

        await _sut.GenerateAsync(filters);

        await _gl.Received().GetByDateRangeAsync(
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_HasBalanceColumn()
    {
        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.Contains(result.Columns, c => c.Header == "Balance");
    }

    // --- Helpers ---

    private void SetupAccounts(params Account[] accounts)
    {
        _accounts.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Account>>(accounts.ToList()));
        _accounts.GetArchivedAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Account>>(Array.Empty<Account>()));
    }

    private void SetupOpeningBalance(Guid accountId, decimal balance)
    {
        _gl.GetAccountBalanceAsync(accountId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(balance);
    }

    private void SetupTransactions(params Transaction[] txns)
    {
        _gl.GetByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Transaction>>(txns.ToList()));
    }

    private static Account MakeAccount(Guid id, string name, AccountType type, string number)
        => new() { Id = id, Name = name, Type = type, AccountNumber = number, CreatedAt = DateTime.UtcNow };

    private static Transaction MakeTransaction(Guid accountId, string desc, decimal debit, decimal credit, DateTime date)
        => new() { Id = Guid.NewGuid(), AccountId = accountId, GLAccount = "x", DebitAmount = debit, CreditAmount = credit, Date = date, Description = desc, CreatedAt = DateTime.UtcNow };

    private static ReportFilterValues CurrentYearFilters()
    {
        var f = new ReportFilterValues();
        f.Set("dateFrom", $"{DateTime.UtcNow.Year}-01-01");
        f.Set("dateTo", $"{DateTime.UtcNow.Year}-12-31");
        return f;
    }
}
