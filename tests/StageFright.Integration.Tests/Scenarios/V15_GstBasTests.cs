using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;
using StageFright.Reports.Providers;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V15: GST-registered income and expense postings, and the
/// BAS Summary report computed from them. Verifies the 3-line taxable posting splits
/// GST to the 2310/2320 clearing accounts, the ledger stays balanced, and BAS labels
/// 1A/1B/G1/G11 match a $110-in/$110-out fixture (1A = 1B = $10).
/// Uses a real SQLite in-memory database with full EF migrations.
/// </summary>
public sealed class V15_GstBasTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid IncomeAccountId = Guid.NewGuid();
    private static readonly Guid ExpenseAccountId = Guid.NewGuid();
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
                Id = IncomeAccountId, Name = "Raffle Income",
                Type = AccountType.Income, AccountNumber = "4000",
                SortOrder = 0, IsSystem = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new Account
            {
                Id = ExpenseAccountId, Name = "Hall Hire",
                Type = AccountType.Expense, AccountNumber = "6000",
                SortOrder = 0, IsSystem = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        await _db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- GST-registered income posting ---

    [Fact]
    public async Task RecordIncome_RegisteredAndTaxable_Posts3Lines_SplitsGstToClearingAccount()
    {
        await SeedSettingsAsync(isGstRegistered: true);
        var svc = BuildIncomeService();

        await svc.RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = Today, Amount = 110m, AccountId = IncomeAccountId, GstCode = GstCode.Gst
        });

        var lines = await _db.Transactions.ToListAsync();
        Assert.Equal(3, lines.Count);
        Assert.Equal(lines.Sum(t => t.DebitAmount), lines.Sum(t => t.CreditAmount));

        var gstLine = Assert.Single(lines, t => t.AccountId == SystemAccounts.GstCollectedId);
        Assert.Equal(10m, gstLine.CreditAmount);
        Assert.All(lines, t => Assert.Equal(GstCode.Gst, t.GstCode));
    }

    // --- GST-registered expense posting ---

    [Fact]
    public async Task RecordExpense_RegisteredAndTaxable_Posts3Lines_SplitsGstToClearingAccount()
    {
        await SeedSettingsAsync(isGstRegistered: true);
        var svc = BuildExpenseService();

        await svc.RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = Today, Amount = 110m,
            BankAccountId = SystemAccounts.CashId,
            ExpenseAccountId = ExpenseAccountId,
            GstCode = GstCode.Gst
        });

        var lines = await _db.Transactions.ToListAsync();
        Assert.Equal(3, lines.Count);
        Assert.Equal(lines.Sum(t => t.DebitAmount), lines.Sum(t => t.CreditAmount));

        var gstLine = Assert.Single(lines, t => t.AccountId == SystemAccounts.GstPaidId);
        Assert.Equal(10m, gstLine.DebitAmount);
        Assert.All(lines, t => Assert.Equal(GstCode.Gst, t.GstCode));
    }

    // --- Toggle-off byte-identical regression ---

    [Fact]
    public async Task RecordIncome_NotRegistered_Posts2Lines_WithNullGstCode_EvenWhenGstCodeRequested()
    {
        await SeedSettingsAsync(isGstRegistered: false);
        var svc = BuildIncomeService();

        await svc.RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = Today, Amount = 110m, AccountId = IncomeAccountId, GstCode = GstCode.Gst
        });

        var lines = await _db.Transactions.ToListAsync();
        Assert.Equal(2, lines.Count);
        Assert.All(lines, t => Assert.Null(t.GstCode));
    }

    // --- BAS Summary report ---

    [Fact]
    public async Task BasSummary_MatchingIncomeAndExpense_1AEquals1BEqualsTen()
    {
        await SeedSettingsAsync(isGstRegistered: true);

        await BuildIncomeService().RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = Today, Amount = 110m, AccountId = IncomeAccountId, GstCode = GstCode.Gst
        });
        await BuildExpenseService().RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = Today, Amount = 110m,
            BankAccountId = SystemAccounts.CashId,
            ExpenseAccountId = ExpenseAccountId,
            GstCode = GstCode.Gst
        });

        var provider = new BasSummaryReportProvider(new GLRepository(_db), new AccountRepository(_db), new SettingsRepository(_db));
        var filters = new StageFright.Reports.Models.ReportFilterValues();
        filters.Set("dateFrom", $"{Today.AddDays(-1):yyyy-MM-dd}");
        filters.Set("dateTo", $"{Today.AddDays(1):yyyy-MM-dd}");

        var result = await provider.GenerateAsync(filters);

        var rows = result.Sections.Single().Rows;
        Assert.Equal("10.00", rows.Single(r => r.Cells[0] == "1A").Cells[2]);
        Assert.Equal("10.00", rows.Single(r => r.Cells[0] == "1B").Cells[2]);
        Assert.Equal("110.00", rows.Single(r => r.Cells[0] == "G1").Cells[2]);
        Assert.Equal("110.00", rows.Single(r => r.Cells[0] == "G11").Cells[2]);
        Assert.Equal("0.00", result.GrandTotal!.Cells[2]);
    }

    [Fact]
    public async Task BasSummary_NotRegistered_SelfExplains()
    {
        await SeedSettingsAsync(isGstRegistered: false);

        var provider = new BasSummaryReportProvider(new GLRepository(_db), new AccountRepository(_db), new SettingsRepository(_db));
        var result = await provider.GenerateAsync(new StageFright.Reports.Models.ReportFilterValues());

        Assert.Empty(result.Sections);
        Assert.Contains("not registered for GST", result.SubTitle);
    }

    // --- Helpers ---

    private IncomeEntryService BuildIncomeService() =>
        new(new AccountRepository(_db), new GLRepository(_db), new JournalEntryRepository(_db),
            new SettingsRepository(_db), BuildAuditService(), new UnitOfWork(_db));

    private ExpensePaymentService BuildExpenseService() =>
        new(new AccountRepository(_db), new GLRepository(_db), new JournalEntryRepository(_db),
            new SettingsRepository(_db), BuildAuditService(), new UnitOfWork(_db));

    private static AuditTrailService BuildAuditService()
    {
        var auditRepo = NSubstitute.Substitute.For<StageFright.Core.Contracts.IAuditTrailRepository>();
        return new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
    }

    private async Task SeedSettingsAsync(bool isGstRegistered)
    {
        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Test Choir",
            AnnualFee = 50m, AttendanceFee = 10m,
            MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
            MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            FinancialYearStartMonth = 7, IsGstRegistered = isGstRegistered,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
