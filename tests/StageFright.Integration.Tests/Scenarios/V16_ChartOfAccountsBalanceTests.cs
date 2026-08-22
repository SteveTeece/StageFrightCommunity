using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;
using StageFright.Reports.Providers;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V16: Chart of Accounts Balance column.
/// Verifies AccountBalanceService's balances against a real SQLite in-memory database,
/// including the credit-normal sign flip and parity with BalanceSheetReportProvider /
/// IGLRepository.GetAccountBalanceAsync for the same accounts (FR-004/SC-003).
/// </summary>
public sealed class V16_ChartOfAccountsBalanceTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid SavingsAccountId = Guid.NewGuid();
    private static readonly Guid DonationsIncomeAccountId = Guid.NewGuid();
    private static readonly Guid VenueHireExpenseAccountId = Guid.NewGuid();
    private static readonly Guid EmptyAccountId = Guid.NewGuid();

    private static readonly DateTime Today = DateTime.UtcNow.Date;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        _db.Accounts.AddRange(
            new Account
            {
                Id = SavingsAccountId, Name = "Savings", Type = AccountType.Asset,
                AccountNumber = "1110", IsBankAccount = true, SortOrder = 0, IsSystem = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new Account
            {
                Id = DonationsIncomeAccountId, Name = "Donations", Type = AccountType.Income,
                AccountNumber = "4001", SortOrder = 0, IsSystem = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new Account
            {
                Id = VenueHireExpenseAccountId, Name = "Venue Hire", Type = AccountType.Expense,
                AccountNumber = "6000", SortOrder = 0, IsSystem = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new Account
            {
                Id = EmptyAccountId, Name = "Brand New Fund", Type = AccountType.Income,
                AccountNumber = "4002", SortOrder = 0, IsSystem = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        // $250 donation deposited into Savings: Debit Savings 250 / Credit Donations 250.
        _db.Transactions.AddRange(
            new Transaction
            {
                Id = Guid.NewGuid(), Date = Today, AccountId = SavingsAccountId, GLAccount = "1110",
                DebitAmount = 250m, CreditAmount = 0m, Description = "Donation deposit", CreatedAt = DateTime.UtcNow
            },
            new Transaction
            {
                Id = Guid.NewGuid(), Date = Today, AccountId = DonationsIncomeAccountId, GLAccount = "4001",
                DebitAmount = 0m, CreditAmount = 250m, Description = "Donation deposit", CreatedAt = DateTime.UtcNow
            },
            // $60 venue hire paid from Savings: Debit Venue Hire 60 / Credit Savings 60.
            new Transaction
            {
                Id = Guid.NewGuid(), Date = Today, AccountId = VenueHireExpenseAccountId, GLAccount = "6000",
                DebitAmount = 60m, CreditAmount = 0m, Description = "Venue hire", CreatedAt = DateTime.UtcNow
            },
            new Transaction
            {
                Id = Guid.NewGuid(), Date = Today, AccountId = SavingsAccountId, GLAccount = "1110",
                DebitAmount = 0m, CreditAmount = 60m, Description = "Venue hire payment", CreatedAt = DateTime.UtcNow
            });

        await _db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task GetActiveAccountBalancesAsync_ReturnsDebitNormalBalance_ForAssetAccount()
    {
        var result = await BuildAccountBalanceService().GetActiveAccountBalancesAsync(TestContext.Current.CancellationToken);

        var savings = Assert.Single(result, r => r.AccountId == SavingsAccountId);
        Assert.Equal(190m, savings.Balance); // 250 debit - 60 credit
        Assert.False(savings.HasError);
    }

    [Fact]
    public async Task GetActiveAccountBalancesAsync_FlipsSign_ForIncomeAccount()
    {
        var result = await BuildAccountBalanceService().GetActiveAccountBalancesAsync(TestContext.Current.CancellationToken);

        var donations = Assert.Single(result, r => r.AccountId == DonationsIncomeAccountId);
        Assert.Equal(250m, donations.Balance); // net credit of 250, displayed positive
    }

    [Fact]
    public async Task GetActiveAccountBalancesAsync_ReturnsDebitNormalBalance_ForExpenseAccount()
    {
        var result = await BuildAccountBalanceService().GetActiveAccountBalancesAsync(TestContext.Current.CancellationToken);

        var venueHire = Assert.Single(result, r => r.AccountId == VenueHireExpenseAccountId);
        Assert.Equal(60m, venueHire.Balance);
    }

    [Fact]
    public async Task GetActiveAccountBalancesAsync_ReturnsZero_ForAccountWithNoTransactions()
    {
        var result = await BuildAccountBalanceService().GetActiveAccountBalancesAsync(TestContext.Current.CancellationToken);

        var empty = Assert.Single(result, r => r.AccountId == EmptyAccountId);
        Assert.Equal(0m, empty.Balance);
        Assert.False(empty.HasError);
    }

    [Fact]
    public async Task GetActiveAccountBalancesAsync_AgreesWithBalanceSheetReportProvider_ForAssetAccount()
    {
        var asAt = DateTime.UtcNow;

        var gl = new GLRepository(_db);
        var expectedNetDebit = await gl.GetAccountBalanceAsync(SavingsAccountId, asAt, TestContext.Current.CancellationToken);

        var result = await BuildAccountBalanceService().GetActiveAccountBalancesAsync(TestContext.Current.CancellationToken);
        var savings = Assert.Single(result, r => r.AccountId == SavingsAccountId);

        // Asset is debit-normal in both AccountBalanceService and BalanceSheetReportProvider
        // (creditNormal: false there too), so the figures must agree exactly.
        Assert.Equal(expectedNetDebit, savings.Balance);

        var balanceSheet = BuildBalanceSheetProvider();
        var report = await balanceSheet.GenerateAsync(new StageFright.Reports.Models.ReportFilterValues(), TestContext.Current.CancellationToken);
        var assetsSection = report.Sections.Single(s => s.Heading == "Assets");
        var savingsRow = assetsSection.Rows.Single(r => r.Cells[0].Contains("Savings"));
        Assert.Contains(savings.Balance!.Value.ToString("F2"), savingsRow.Cells[1]);
    }

    // --- Helpers ---

    private AccountBalanceService BuildAccountBalanceService()
    {
        var accountRepo = new AccountRepository(_db);
        var gl = new GLRepository(_db);
        return new AccountBalanceService(accountRepo, gl, NullLogger<AccountBalanceService>.Instance);
    }

    private BalanceSheetReportProvider BuildBalanceSheetProvider()
    {
        var gl = new GLRepository(_db);
        var accounts = new AccountRepository(_db);
        var settings = new SettingsRepository(_db);
        return new BalanceSheetReportProvider(gl, accounts, settings);
    }
}
