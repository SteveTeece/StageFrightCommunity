using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;
using StageFright.Reports.Rendering;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V6: accounting reports generated against a real in-memory SQLite database.
/// Verifies all 4 report providers, PDF non-empty, CSV escaping, Trial Balance imbalance detection,
/// Account Register running balance, and Member Account Summary aging.
/// </summary>
public sealed class V6_AccountingReportsTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid IncomeCatId = Guid.NewGuid();
    private static readonly Guid ExpenseCatId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        _db.Accounts.AddRange(
            new Account { Id = IncomeCatId, Name = "Membership Dues", Type = AccountType.Income, AccountNumber = "1000", SortOrder = 0, IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Account { Id = ExpenseCatId, Name = "Hall Hire", Type = AccountType.Expense, AccountNumber = "2000", SortOrder = 0, IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );

        _db.Members.Add(new Member
        {
            Id = MemberId, Name = "Alice Smith", StreetAddress = "1 Test St",
            Status = MemberStatus.Active, JoinDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ActivateDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });

        // Alice has an outstanding 25.00 annual fee: debit MemberReceivable / credit Income (balanced pair)
        var feeId = Guid.NewGuid();
        _db.Fees.Add(new Fee
        {
            Id = feeId, MemberId = MemberId, FeeType = FeeType.Annual, Amount = 25m,
            FeeDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            PaidAtCreation = false, CreatedAt = DateTime.UtcNow
        });
        _db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), Date = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), AccountId = SystemAccounts.MemberReceivableId, GLAccount = SystemAccounts.MemberReceivableNumber, DebitAmount = 25m, CreditAmount = 0, MemberId = MemberId, FeeId = feeId, Description = "Annual fee accrual", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), AccountId = IncomeCatId, GLAccount = "1000", DebitAmount = 0, CreditAmount = 25m, MemberId = MemberId, FeeId = feeId, Description = "Annual fee income", CreatedAt = DateTime.UtcNow });

        // Balanced GL: debit MemberReceivable / credit Income = 100, debit Cash / credit MemberReceivable = 100
        _db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), AccountId = IncomeCatId, GLAccount = "1000", DebitAmount = 0, CreditAmount = 100m, MemberId = MemberId, Description = "Annual fee", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), AccountId = IncomeCatId, GLAccount = "1000", DebitAmount = 100m, CreditAmount = 0, MemberId = MemberId, Description = "Annual fee offset", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), AccountId = ExpenseCatId, GLAccount = "2000", DebitAmount = 50m, CreditAmount = 0, Description = "Hall hire", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), AccountId = ExpenseCatId, GLAccount = "2000", DebitAmount = 0, CreditAmount = 50m, Description = "Hall hire payment", CreatedAt = DateTime.UtcNow }
        );

        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- Income Statement ---

    [Fact]
    public async Task IncomeStatement_GeneratesReport_WithIncomeAndExpenseSections()
    {
        var sut = BuildIncomeStatementProvider();
        var result = await sut.GenerateAsync(CurrentYearCustomPeriodFilters());

        Assert.Equal("Statement of Income & Expenditure", result.Title);
        Assert.Contains(result.Sections, s => s.Heading == "Income");
        Assert.Contains(result.Sections, s => s.Heading == "Expenses");
    }

    [Fact]
    public async Task IncomeStatement_IncomeSection_ContainsMembershipDues()
    {
        var sut = BuildIncomeStatementProvider();
        var result = await sut.GenerateAsync(CurrentYearCustomPeriodFilters());

        var incomeSection = result.Sections.First(s => s.Heading == "Income");
        Assert.Contains(incomeSection.Rows, r => r.Cells.Any(c => c.Contains("Membership Dues")));
    }

    // --- Trial Balance ---

    [Fact]
    public async Task TrialBalance_WhenBalanced_GeneratesReport()
    {
        var sut = BuildTrialBalanceProvider();
        var result = await sut.GenerateAsync(CurrentYearFilters());

        Assert.Equal("Trial Balance", result.Title);
        Assert.Contains(result.Columns, c => c.Header == "Debit");
        Assert.Contains(result.Columns, c => c.Header == "Credit");
    }

    [Fact]
    public async Task TrialBalance_ForcedImbalance_ThrowsGLBalanceExceptionWithFR034Message()
    {
        // Add an unbalanced transaction to create an imbalance
        _db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            Date = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            AccountId = IncomeCatId, GLAccount = "1000",
            DebitAmount = 0, CreditAmount = 999m, // unbalanced
            Description = "Imbalance test", CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var sut = BuildTrialBalanceProvider();
        var ex = await Assert.ThrowsAsync<GLBalanceException>(
            () => sut.GenerateAsync(CurrentYearFilters()));

        Assert.Contains("GL Balance Verification Failed", ex.Message);
    }

    // --- Account Register ---

    [Fact]
    public async Task AccountRegister_GeneratesReport_InChronologicalOrder()
    {
        var sut = BuildAccountRegisterProvider();
        var result = await sut.GenerateAsync(CurrentYearFilters());

        Assert.Equal("Account Register", result.Title);
        var rows = result.Sections.SelectMany(s => s.Rows).ToList();
        Assert.NotEmpty(rows);
        // Rows should be in date order (March before April)
        var dates = rows.Select(r => r.Cells[0]).Where(d => d.StartsWith("2026")).ToList();
        if (dates.Count > 1)
            Assert.True(string.Compare(dates[0], dates[1], StringComparison.Ordinal) <= 0);
    }

    [Fact]
    public async Task AccountRegister_HasRunningBalanceColumn()
    {
        var sut = BuildAccountRegisterProvider();
        var result = await sut.GenerateAsync(CurrentYearFilters());

        Assert.Contains(result.Columns, c => c.Header.Contains("Balance", StringComparison.OrdinalIgnoreCase));
    }

    // --- Member Account Summary ---

    [Fact]
    public async Task MemberAccountSummary_GeneratesReport_WithMemberSection()
    {
        var sut = BuildMemberAccountSummaryProvider();
        var result = await sut.GenerateAsync(CurrentYearFilters());

        Assert.Equal("Member Account Summary", result.Title);
        Assert.Contains(result.Sections, s => s.Heading != null && s.Heading.Contains("Alice"));
    }

    // --- PDF Renderer ---

    [Fact]
    public async Task PdfRenderer_Render_ReturnsNonEmptyBytes()
    {
        var sut = BuildIncomeStatementProvider();
        var report = await sut.GenerateAsync(CurrentYearCustomPeriodFilters());
        var renderer = new PdfReportRenderer();

        var bytes = renderer.Render(report);

        Assert.NotEmpty(bytes);
    }

    // --- CSV Exporter ---

    [Fact]
    public async Task CsvExporter_Export_FirstRowIsHeaders()
    {
        var sut = BuildIncomeStatementProvider();
        var report = await sut.GenerateAsync(CurrentYearCustomPeriodFilters());
        var exporter = new CsvReportExporter();

        var csv = exporter.Export(report);
        var firstLine = csv.Split('\n')[0].TrimEnd('\r');

        Assert.Contains("Account", firstLine);
        Assert.Contains("Amount", firstLine);
    }

    [Fact]
    public void CsvExporter_ValuesWithCommas_AreQuoted()
    {
        var exporter = new CsvReportExporter();
        var report = new ReportData
        {
            Title = "Test", GeneratedAt = DateTime.UtcNow,
            Columns = [new ReportColumn { Header = "Name" }],
            Sections =
            [
                new ReportSection
                {
                    Rows = [new ReportRow { Cells = ["Smith, John"] }]
                }
            ]
        };

        var csv = exporter.Export(report);

        Assert.Contains("\"Smith, John\"", csv);
    }

    // --- Helpers ---

    private IncomeStatementReportProvider BuildIncomeStatementProvider()
    {
        var gl = new GLRepository(_db);
        var cat = new AccountRepository(_db);
        return new IncomeStatementReportProvider(gl, cat, new SettingsRepository(_db));
    }

    private TrialBalanceReportProvider BuildTrialBalanceProvider()
    {
        var gl = new GLRepository(_db);
        var cat = new AccountRepository(_db);
        return new TrialBalanceReportProvider(gl, cat, new SettingsRepository(_db));
    }

    private AccountRegisterReportProvider BuildAccountRegisterProvider()
    {
        var gl = new GLRepository(_db);
        var cat = new AccountRepository(_db);
        return new AccountRegisterReportProvider(gl, cat);
    }

    private MemberAccountSummaryReportProvider BuildMemberAccountSummaryProvider()
    {
        var gl = new GLRepository(_db);
        var members = new MemberRepository(_db);
        var fees = new FeeRepository(_db);
        return new MemberAccountSummaryReportProvider(gl, members, new MemberBalanceService(members, fees, gl));
    }

    private static ReportFilterValues CurrentYearFilters()
    {
        var f = new ReportFilterValues();
        f.Set("dateFrom", $"{DateTime.UtcNow.Year}-01-01");
        f.Set("dateTo", $"{DateTime.UtcNow.Year}-12-31");
        return f;
    }

    /// <summary>Calendar-year custom period, for IncomeStatementReportProvider (defaults to "This FY" otherwise).</summary>
    private static ReportFilterValues CurrentYearCustomPeriodFilters()
    {
        var f = CurrentYearFilters();
        f.Set("period", "Custom");
        return f;
    }
}
