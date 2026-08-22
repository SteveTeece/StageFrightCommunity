using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for TaxSummaryReportProvider:
/// - Metadata (id, module, order)
/// - Self-explains when sales tax doesn't apply to the organisation
/// - Total taxable sales / total tax-exempt sales / tax collected / tax paid / net computed from a known-numbers fixture ledger
/// </summary>
public class TaxSummaryReportProviderTests
{
    private readonly IGLRepository _gl = Substitute.For<IGLRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ISettingsRepository _settings = Substitute.For<ISettingsRepository>();
    private readonly TaxSummaryReportProvider _sut;

    private static readonly Guid IncomeAccountId = Guid.NewGuid();
    private static readonly Guid ExpenseAccountId = Guid.NewGuid();
    private static readonly DateTime From = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

    public TaxSummaryReportProviderTests()
    {
        _accounts.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                MakeAccount(IncomeAccountId, AccountType.Income, "4000"),
                MakeAccount(ExpenseAccountId, AccountType.Expense, "6000")
            });
        _accounts.GetArchivedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>());

        _sut = new TaxSummaryReportProvider(_gl, _accounts, _settings);
    }

    // --- Metadata ---

    [Fact]
    public void ReportId_IsTaxSummary()
    {
        Assert.Equal("tax-summary", _sut.ReportId);
    }

    [Fact]
    public void ModuleName_IsFinance_And_DisplayOrderIs60()
    {
        Assert.Equal("Finance", _sut.ModuleName);
        Assert.Equal(60, _sut.DisplayOrder);
    }

    // --- Not applicable ---

    [Fact]
    public async Task GenerateAsync_NoSettings_ProducesEmptyReportWithExplanation()
    {
        _settings.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);

        var result = await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        Assert.Empty(result.Sections);
        Assert.Contains("does not apply", result.SubTitle);
    }

    [Fact]
    public async Task GenerateAsync_TaxNotApplicable_ProducesEmptyReportWithExplanation()
    {
        _settings.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isTaxApplicable: false));

        var result = await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        Assert.Empty(result.Sections);
        Assert.Contains("does not apply", result.SubTitle);
    }

    // --- Known-numbers fixture ---

    [Fact]
    public async Task GenerateAsync_TaxApplicable_ComputesTaxSummary_FromFixtureLedger()
    {
        _settings.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isTaxApplicable: true));

        _gl.GetByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>
            {
                // Taxable sale: net $100 credited to Income (tax of $10 goes to the 2310 clearing account separately)
                MakeLine(IncomeAccountId, credit: 100m, taxCode: TaxCode.Taxable),
                // Tax-exempt sale: net $50 credited to Income, no tax component
                MakeLine(IncomeAccountId, credit: 50m, taxCode: TaxCode.TaxExempt),
                // Taxable purchase: net $80 debited to Expense (tax of $8 goes to the 2320 clearing account separately)
                MakeLine(ExpenseAccountId, debit: 80m, taxCode: TaxCode.Taxable)
            });

        _gl.GetAccountMovementsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, (decimal Debits, decimal Credits)>
            {
                [SystemAccounts.TaxCollectedId] = (0m, 10m),
                [SystemAccounts.TaxPaidId] = (8m, 0m)
            });

        var filters = new ReportFilterValues();
        filters.Set("dateFrom", $"{From:yyyy-MM-dd}");
        filters.Set("dateTo", $"{To:yyyy-MM-dd}");

        var result = await _sut.GenerateAsync(filters, TestContext.Current.CancellationToken);

        var rows = result.Sections.Single().Rows;
        Assert.Equal("160.00", rows.Single(r => r.Cells[0] == "Total taxable sales").Cells[1]);   // 150 coded income + 10 tax on sales
        Assert.Equal("50.00", rows.Single(r => r.Cells[0] == "Total tax-exempt sales").Cells[1]);  // tax-exempt sales only
        Assert.Equal("10.00", rows.Single(r => r.Cells[0] == "Tax collected on sales").Cells[1]);
        Assert.Equal("8.00", rows.Single(r => r.Cells[0] == "Tax paid on purchases").Cells[1]);

        Assert.NotNull(result.GrandTotal);
        Assert.Equal("2.00", result.GrandTotal!.Cells[1]);
        Assert.Contains("payable", result.GrandTotal.Cells[0]);
    }

    [Fact]
    public async Task GenerateAsync_TaxApplicable_NetTaxNegative_ShowsRefundable()
    {
        _settings.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isTaxApplicable: true));

        _gl.GetByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>());

        _gl.GetAccountMovementsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, (decimal Debits, decimal Credits)>
            {
                [SystemAccounts.TaxCollectedId] = (0m, 5m),
                [SystemAccounts.TaxPaidId] = (20m, 0m)
            });

        var filters = new ReportFilterValues();
        filters.Set("dateFrom", $"{From:yyyy-MM-dd}");
        filters.Set("dateTo", $"{To:yyyy-MM-dd}");

        var result = await _sut.GenerateAsync(filters, TestContext.Current.CancellationToken);

        Assert.Equal("15.00", result.GrandTotal!.Cells[1]);
        Assert.Contains("refundable", result.GrandTotal.Cells[0]);
    }

    // --- Helpers ---

    private static Settings MakeSettings(bool isTaxApplicable) => new()
    {
        Id = Guid.NewGuid(), OrganizationName = "Test Choir",
        AnnualFee = 50m, AttendanceFee = 10m,
        MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
        MinimumMemberAge = 0, SchemaVersion = "1.1.0",
        FinancialYearStartMonth = 7, IsTaxApplicable = isTaxApplicable,
        TaxRate = isTaxApplicable ? 10m : null,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Account MakeAccount(Guid id, AccountType type, string number) => new()
    {
        Id = id, Name = number, Type = type, AccountNumber = number,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Transaction MakeLine(Guid accountId, decimal debit = 0m, decimal credit = 0m, TaxCode? taxCode = null) => new()
    {
        Id = Guid.NewGuid(), AccountId = accountId, Date = From.AddDays(1),
        DebitAmount = debit, CreditAmount = credit, GLAccount = "0000",
        TaxCode = taxCode, CreatedAt = DateTime.UtcNow
    };
}
