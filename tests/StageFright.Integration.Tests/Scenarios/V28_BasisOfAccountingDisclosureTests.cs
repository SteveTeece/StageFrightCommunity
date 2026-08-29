using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;
using StageFright.Reports.Rendering;
using StageFright.Reports.Resources;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// V28 acceptance — US4 AC-1…AC-2 (spec 028): the Income Statement, Balance Sheet and Tax
/// Summary each carry a basis-of-accounting line on screen (<see cref="ReportData.BasisOfAccounting"/>),
/// in the exported CSV, and in the rendered PDF. The wording names both the accrual treatment
/// of member fees and the cash treatment of other income and expenditure — it is not a single
/// blanket basis. Real in-memory SQLite + full migrations.
/// </summary>
[Collection("MoneyFormatterState")]
public sealed class V28_BasisOfAccountingDisclosureTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid IncomeId = Guid.NewGuid();
    private static readonly Guid ExpenseId = Guid.NewGuid();
    private static readonly DateTime PostDate = new(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);

    private static readonly ILocalizer L = new Localizer(
        new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance));

    private static string ExpectedBasis => L.Get<ReportsResource>("Reports_Common_BasisOfAccounting");

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        _db.Accounts.AddRange(
            new Account { Id = IncomeId, Name = "Membership Dues", Type = AccountType.Income, AccountNumber = "4000", SortOrder = 0, IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Account { Id = ExpenseId, Name = "Hall Hire", Type = AccountType.Expense, AccountNumber = "6000", SortOrder = 0, IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        // Balanced pairs: DR Cash / CR Income, and DR Expense / CR Cash.
        _db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), Date = PostDate, AccountId = SystemAccounts.CashId, GLAccount = "1100", DebitAmount = 1_000m, CreditAmount = 0m, Description = "Dues receipt", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = PostDate, AccountId = IncomeId, GLAccount = "4000", DebitAmount = 0m, CreditAmount = 1_000m, Description = "Dues income", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = PostDate, AccountId = ExpenseId, GLAccount = "6000", DebitAmount = 400m, CreditAmount = 0m, Description = "Hall hire", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = PostDate, AccountId = SystemAccounts.CashId, GLAccount = "1100", DebitAmount = 0m, CreditAmount = 400m, Description = "Hall hire payment", CreatedAt = DateTime.UtcNow });

        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Disclosure Players",
            AnnualFee = 50m, AttendanceFee = 10m, MembershipRenewalMonth = 1,
            MaxAgeRangeYears = 150, MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            FinancialYearStartMonth = 7, FinancialYearStartDay = 1,
            CurrencyCode = "AUD", IsTaxApplicable = true,
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

    // AC-2: the shared wording names both treatments — never one blanket basis.
    [Fact]
    public void SharedBasisWording_NamesBothTheAccrualAndCashTreatments()
    {
        var basis = ExpectedBasis;

        Assert.False(string.IsNullOrWhiteSpace(basis));
        Assert.NotEqual("Reports_Common_BasisOfAccounting", basis);
        Assert.Contains("accrual", basis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cash", basis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("member", basis, StringComparison.OrdinalIgnoreCase);
    }

    // AC-1: on screen — every statement's ReportData carries the basis line.
    [Fact]
    public async Task IncomeStatement_BalanceSheet_TaxSummary_EachCarryTheBasisLineOnScreen()
    {
        foreach (var report in await GenerateAllAsync())
            Assert.Equal(ExpectedBasis, report.BasisOfAccounting);
    }

    // AC-1: in the exported CSV.
    [Fact]
    public async Task EveryStatement_CarriesTheBasisLine_InTheExportedCsv()
    {
        var exporter = new CsvReportExporter();

        foreach (var report in await GenerateAllAsync())
        {
            var csv = exporter.Export(report);
            Assert.Contains("accrual", csv, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("cash", csv, StringComparison.OrdinalIgnoreCase);

            var lastRecord = csv.Split('\n').Last(l => !string.IsNullOrWhiteSpace(l));
            Assert.Contains("accrual", lastRecord, StringComparison.OrdinalIgnoreCase);
        }
    }

    // AC-1: in the rendered PDF (bytes produced without error).
    [Fact]
    public async Task EveryStatement_RendersToPdf_WithTheBasisLine()
    {
        var renderer = new PdfReportRenderer(L);

        foreach (var report in await GenerateAllAsync())
        {
            Assert.NotNull(report.BasisOfAccounting);
            var bytes = renderer.Render(report, "Disclosure Players");
            Assert.NotEmpty(bytes);
        }
    }

    private async Task<IReadOnlyList<ReportData>> GenerateAllAsync()
    {
        var gl = new GLRepository(_db);
        var accounts = new AccountRepository(_db);
        var settings = new SettingsRepository(_db);
        var ct = TestContext.Current.CancellationToken;

        return
        [
            await new IncomeStatementReportProvider(gl, accounts, settings, L).GenerateAsync(RangeFilters(), ct),
            await new BalanceSheetReportProvider(gl, accounts, settings, L).GenerateAsync(AsAtFilters(), ct),
            await new TaxSummaryReportProvider(gl, accounts, settings, L).GenerateAsync(RangeFilters(), ct)
        ];
    }

    private static ReportFilterValues RangeFilters()
    {
        var f = new ReportFilterValues();
        f.Set("dateFrom", "2026-03-01");
        f.Set("dateTo", "2026-03-31");
        return f;
    }

    private static ReportFilterValues AsAtFilters()
    {
        var f = new ReportFilterValues();
        f.Set("asAt", "2026-03-31");
        return f;
    }
}
