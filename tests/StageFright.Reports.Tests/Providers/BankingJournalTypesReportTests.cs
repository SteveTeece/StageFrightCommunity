using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;

namespace StageFright.Reports.Tests.Providers;

/// <summary>
/// Spec 033 FR-021: historical <see cref="JournalEntryType.Transfer"/> and
/// <see cref="JournalEntryType.BankDeposit"/> entries, plus the new
/// <see cref="JournalEntryType.BankDepositReversal"/> and <see cref="JournalEntryType.CashWithdrawal"/>
/// entries, render correctly in the General Ledger, Account Register, Trial Balance and
/// Balance Sheet, with Undeposited Funds (1105) under Assets.
/// The GL repository is backed by one in-memory ledger so every report reads the same postings.
/// </summary>
public class BankingJournalTypesReportTests
{
    private static readonly Guid BankId = Guid.NewGuid();
    private static readonly Guid IncomeId = Guid.NewGuid();

    private readonly IGLRepository _gl = Substitute.For<IGLRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ISettingsRepository _settings = Substitute.For<ISettingsRepository>();
    private readonly List<Transaction> _ledger = [];

    public BankingJournalTypesReportTests()
    {
        _accounts.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Account>>(
        [
            MakeAccount(SystemAccounts.CashId, "Petty Cash", AccountType.Asset, SystemAccounts.CashNumber, isSystem: true, isBank: true),
            MakeAccount(SystemAccounts.UndepositedFundsId, "Undeposited Funds", AccountType.Asset, SystemAccounts.UndepositedFundsNumber, isSystem: true),
            MakeAccount(BankId, "Main Bank", AccountType.Asset, "1110", isBank: true),
            MakeAccount(SystemAccounts.MemberReceivableId, "Member Receivable", AccountType.Asset, SystemAccounts.MemberReceivableNumber, isSystem: true),
            MakeAccount(SystemAccounts.OpeningBalanceEquityId, "Opening Balance Equity", AccountType.Equity, SystemAccounts.OpeningBalanceEquityNumber, isSystem: true),
            MakeAccount(IncomeId, "Sponsorship", AccountType.Income, "4100")
        ]));
        _accounts.GetArchivedAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Account>>(Array.Empty<Account>()));
        _settings.GetAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<Settings?>(null));

        WireLedger();
        SeedLedger();
    }

    // --- General Ledger ---

    [Fact]
    public async Task Should_ShowEveryBankingEntryWithRunningBalance_When_GeneralLedgerShowsTheBankAccount()
    {
        var sut = new GeneralLedgerReportProvider(_gl, _accounts, RealLocalizer.Instance);

        var result = await sut.GenerateAsync(YearFilters(), TestContext.Current.CancellationToken);

        var bank = result.Sections.Single(s => s.Heading == "Main Bank (1110)");
        var descriptions = bank.Rows.Select(r => r.Cells[1]).ToList();
        Assert.Equal(
            ["Opening Balance", "Legacy transfer", "Legacy bank deposit", "Bank deposit", "Bank deposit reversal", "Cash withdrawal"],
            descriptions);
        // 0 → +50 → +100 → +330 → −330 → −40
        Assert.Equal(
            [MoneyFormatter.Format(0m), MoneyFormatter.Format(50m), MoneyFormatter.Format(150m),
             MoneyFormatter.Format(480m), MoneyFormatter.Format(150m), MoneyFormatter.Format(110m)],
            bank.Rows.Select(r => r.Cells[4]).ToList());
        Assert.Equal(MoneyFormatter.Format(110m), bank.Subtotal!.Cells[4]);
    }

    [Fact]
    public async Task Should_ShowUndepositedFundsSection_When_GeneralLedgerHasChequeDepositAndReversal()
    {
        var sut = new GeneralLedgerReportProvider(_gl, _accounts, RealLocalizer.Instance);

        var result = await sut.GenerateAsync(YearFilters(), TestContext.Current.CancellationToken);

        var undeposited = result.Sections.Single(s => s.Heading == "Undeposited Funds (1105)");
        Assert.Equal(
            ["Opening Balance", "Cheque sponsorship", "Bank deposit", "Bank deposit reversal"],
            undeposited.Rows.Select(r => r.Cells[1]).ToList());
        Assert.Equal(MoneyFormatter.Format(300m), undeposited.Rows[2].Cells[3]); // deposit credits the cheques
        Assert.Equal(MoneyFormatter.Format(300m), undeposited.Rows[3].Cells[2]); // reversal debits them back
        Assert.Equal(MoneyFormatter.Format(300m), undeposited.Subtotal!.Cells[4]);
    }

    [Fact]
    public async Task Should_ShowPettyCashMovements_When_GeneralLedgerHasDepositReversalAndWithdrawal()
    {
        var sut = new GeneralLedgerReportProvider(_gl, _accounts, RealLocalizer.Instance);

        var result = await sut.GenerateAsync(YearFilters(), TestContext.Current.CancellationToken);

        var pettyCash = result.Sections.Single(s => s.Heading == "Petty Cash (1100)");
        // 200 opening − 50 transfer − 100 legacy deposit − 30 deposit + 30 reversal + 40 withdrawal
        Assert.Equal(MoneyFormatter.Format(90m), pettyCash.Subtotal!.Cells[4]);
        var withdrawal = pettyCash.Rows.Single(r => r.Cells[1] == "Cash withdrawal");
        Assert.Equal(MoneyFormatter.Format(40m), withdrawal.Cells[2]);
    }

    // --- Account Register ---

    [Fact]
    public async Task Should_ListEveryLineOfEveryBankingEntry_When_AccountRegisterGenerated()
    {
        var sut = new AccountRegisterReportProvider(_gl, _accounts, RealLocalizer.Instance);

        var result = await sut.GenerateAsync(YearFilters(), TestContext.Current.CancellationToken);

        var rows = Assert.Single(result.Sections).Rows;
        Assert.Equal(_ledger.Count, rows.Count);
        Assert.Contains(rows, r => r.Cells[1] == "Bank deposit" && r.Cells[2] == "Undeposited Funds" && r.Cells[4] == MoneyFormatter.Format(300m));
        Assert.Contains(rows, r => r.Cells[1] == "Bank deposit reversal" && r.Cells[2] == "Main Bank" && r.Cells[4] == MoneyFormatter.Format(330m));
        Assert.Contains(rows, r => r.Cells[1] == "Cash withdrawal" && r.Cells[2] == "Petty Cash" && r.Cells[3] == MoneyFormatter.Format(40m));
        Assert.Contains(rows, r => r.Cells[1] == "Legacy transfer" && r.Cells[2] == "Main Bank");
        Assert.Contains(rows, r => r.Cells[1] == "Legacy bank deposit" && r.Cells[2] == "Petty Cash");
        // Balanced postings bring the register's running balance back to zero.
        Assert.Equal(MoneyFormatter.Format(0m), rows[^1].Cells[5]);
    }

    // --- Trial Balance ---

    [Fact]
    public async Task Should_BalanceAndListUndepositedFundsUnderAssets_When_TrialBalanceGenerated()
    {
        var sut = new TrialBalanceReportProvider(_gl, _accounts, _settings, RealLocalizer.Instance);

        var result = await sut.GenerateAsync(YearFilters(), TestContext.Current.CancellationToken);

        var assets = result.Sections.Single(s => s.Heading == "Assets");
        var undeposited = assets.Rows.Single(r => r.Cells[0] == "Undeposited Funds (1105)");
        Assert.Equal(MoneyFormatter.Format(600m), undeposited.Cells[1]); // cheque + reversal
        Assert.Equal(MoneyFormatter.Format(300m), undeposited.Cells[2]); // deposit
        var bank = assets.Rows.Single(r => r.Cells[0] == "Main Bank (1110)");
        Assert.Equal(MoneyFormatter.Format(480m), bank.Cells[1]);
        Assert.Equal(MoneyFormatter.Format(370m), bank.Cells[2]);
        Assert.DoesNotContain(
            result.Sections.Where(s => s.Heading != "Assets").SelectMany(s => s.Rows),
            r => r.Cells[0].Contains("1105"));

        var total = _ledger.Sum(t => t.DebitAmount);
        Assert.Equal(MoneyFormatter.Format(total), result.GrandTotal!.Cells[1]);
        Assert.Equal(MoneyFormatter.Format(total), result.GrandTotal.Cells[2]);
    }

    // --- Balance Sheet ---

    [Fact]
    public async Task Should_ShowUndepositedFundsUnderAssetsAndBalance_When_BalanceSheetGenerated()
    {
        var sut = new BalanceSheetReportProvider(_gl, _accounts, _settings, RealLocalizer.Instance);
        var filters = new ReportFilterValues();
        filters.Set("asAt", $"{Year}-12-31");

        var result = await sut.GenerateAsync(filters, TestContext.Current.CancellationToken);

        var assets = result.Sections.Single(s => s.Heading == "Assets");
        Assert.Contains(assets.Rows, r => r.Cells[0] == "Undeposited Funds (1105)" && r.Cells[1] == MoneyFormatter.Format(300m));
        Assert.Contains(assets.Rows, r => r.Cells[0] == "Petty Cash (1100)" && r.Cells[1] == MoneyFormatter.Format(90m));
        Assert.Contains(assets.Rows, r => r.Cells[0] == "Main Bank (1110)" && r.Cells[1] == MoneyFormatter.Format(110m));
        Assert.Equal(MoneyFormatter.Format(500m), assets.Subtotal!.Cells[1]);

        // Assets = Liabilities + Equity (200 opening equity + 300 surplus): no out-of-balance line.
        Assert.Equal(3, result.Sections.Count);
        Assert.Equal(MoneyFormatter.Format(500m), result.GrandTotal!.Cells[1]);
    }

    // --- Ledger ---

    private static int Year => DateTime.UtcNow.Year;

    /// <summary>
    /// One balanced set per journal entry, in date order:
    /// opening balance, a legacy transfer and legacy deposit from Petty Cash, a cheque
    /// sponsorship into Undeposited Funds, a deposit of that cheque plus cash, its reversal,
    /// and a cash withdrawal.
    /// </summary>
    private void SeedLedger()
    {
        Post(JournalEntryType.OpeningBalance, 1, "Opening balance",
            (SystemAccounts.CashId, SystemAccounts.CashNumber, 200m, 0m),
            (SystemAccounts.OpeningBalanceEquityId, SystemAccounts.OpeningBalanceEquityNumber, 0m, 200m));
        Post(JournalEntryType.Transfer, 2, "Legacy transfer",
            (BankId, "1110", 50m, 0m),
            (SystemAccounts.CashId, SystemAccounts.CashNumber, 0m, 50m));
        Post(JournalEntryType.BankDeposit, 3, "Legacy bank deposit",
            (BankId, "1110", 100m, 0m),
            (SystemAccounts.CashId, SystemAccounts.CashNumber, 0m, 100m));
        Post(JournalEntryType.Income, 4, "Cheque sponsorship",
            (SystemAccounts.UndepositedFundsId, SystemAccounts.UndepositedFundsNumber, 300m, 0m),
            (IncomeId, "4100", 0m, 300m));
        Post(JournalEntryType.BankDeposit, 5, "Bank deposit",
            (BankId, "1110", 330m, 0m),
            (SystemAccounts.UndepositedFundsId, SystemAccounts.UndepositedFundsNumber, 0m, 300m),
            (SystemAccounts.CashId, SystemAccounts.CashNumber, 0m, 30m));
        Post(JournalEntryType.BankDepositReversal, 6, "Bank deposit reversal",
            (SystemAccounts.UndepositedFundsId, SystemAccounts.UndepositedFundsNumber, 300m, 0m),
            (SystemAccounts.CashId, SystemAccounts.CashNumber, 30m, 0m),
            (BankId, "1110", 0m, 330m));
        Post(JournalEntryType.CashWithdrawal, 7, "Cash withdrawal",
            (SystemAccounts.CashId, SystemAccounts.CashNumber, 40m, 0m),
            (BankId, "1110", 0m, 40m));

        Assert.Equal(_ledger.Sum(t => t.DebitAmount), _ledger.Sum(t => t.CreditAmount));
    }

    private void Post(JournalEntryType type, int month, string description,
        params (Guid AccountId, string Number, decimal Debit, decimal Credit)[] lines)
    {
        var date = new DateTime(Year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var entry = new JournalEntry { Id = Guid.NewGuid(), Type = type, Date = date, Description = description, CreatedAt = date };
        foreach (var line in lines)
        {
            _ledger.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                Date = date,
                AccountId = line.AccountId,
                GLAccount = line.Number,
                DebitAmount = line.Debit,
                CreditAmount = line.Credit,
                JournalEntryId = entry.Id,
                JournalEntry = entry,
                Description = description,
                CreatedAt = date.AddTicks(_ledger.Count)
            });
        }
    }

    private void WireLedger()
    {
        _gl.GetByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<Transaction>>(
                InRange(ci.ArgAt<DateTime>(0), ci.ArgAt<DateTime>(1)).ToList()));

        _gl.GetAccountBalanceAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(ci => _ledger
                .Where(t => t.AccountId == ci.ArgAt<Guid>(0) && t.Date <= ci.ArgAt<DateTime>(1))
                .Sum(t => t.DebitAmount - t.CreditAmount));

        _gl.GetBalanceTotalsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var lines = InRange(ci.ArgAt<DateTime>(0), ci.ArgAt<DateTime>(1)).ToList();
                return (lines.Sum(t => t.DebitAmount), lines.Sum(t => t.CreditAmount));
            });

        _gl.GetAccountMovementsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyDictionary<Guid, (decimal Debits, decimal Credits)>>(
                InRange(ci.ArgAt<DateTime>(0), ci.ArgAt<DateTime>(1))
                    .GroupBy(t => t.AccountId)
                    .ToDictionary(g => g.Key, g => (g.Sum(t => t.DebitAmount), g.Sum(t => t.CreditAmount)))));
    }

    private IEnumerable<Transaction> InRange(DateTime from, DateTime to) =>
        _ledger.Where(t => t.Date >= from && t.Date <= to);

    private static Account MakeAccount(Guid id, string name, AccountType type, string number,
        bool isSystem = false, bool isBank = false) => new()
    {
        Id = id, Name = name, Type = type, AccountNumber = number,
        IsSystem = isSystem, IsBankAccount = isBank, CreatedAt = DateTime.UtcNow
    };

    private static ReportFilterValues YearFilters()
    {
        var f = new ReportFilterValues();
        f.Set("dateFrom", $"{Year}-01-01");
        f.Set("dateTo", $"{Year}-12-31");
        return f;
    }
}
