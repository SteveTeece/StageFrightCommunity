using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for BasSummaryReportProvider:
/// - Metadata (id, module, order)
/// - Self-explains when the organisation is not GST registered
/// - G1/G3/G11/1A/1B/9 computed from a known-numbers fixture ledger
/// </summary>
public class BasSummaryReportProviderTests
{
    private readonly IGLRepository _gl = Substitute.For<IGLRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ISettingsRepository _settings = Substitute.For<ISettingsRepository>();
    private readonly BasSummaryReportProvider _sut;

    private static readonly Guid IncomeAccountId = Guid.NewGuid();
    private static readonly Guid ExpenseAccountId = Guid.NewGuid();
    private static readonly DateTime From = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

    public BasSummaryReportProviderTests()
    {
        _accounts.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                MakeAccount(IncomeAccountId, AccountType.Income, "4000"),
                MakeAccount(ExpenseAccountId, AccountType.Expense, "6000")
            });
        _accounts.GetArchivedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>());

        _sut = new BasSummaryReportProvider(_gl, _accounts, _settings);
    }

    // --- Metadata ---

    [Fact]
    public void ReportId_IsBasSummary()
    {
        Assert.Equal("bas-summary", _sut.ReportId);
    }

    [Fact]
    public void ModuleName_IsFinance_And_DisplayOrderIs60()
    {
        Assert.Equal("Finance", _sut.ModuleName);
        Assert.Equal(60, _sut.DisplayOrder);
    }

    // --- Not registered ---

    [Fact]
    public async Task GenerateAsync_NotRegistered_ProducesEmptyReportWithExplanation()
    {
        _settings.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);

        var result = await _sut.GenerateAsync(new ReportFilterValues());

        Assert.Empty(result.Sections);
        Assert.Contains("not registered for GST", result.SubTitle);
    }

    [Fact]
    public async Task GenerateAsync_RegisteredFalse_ProducesEmptyReportWithExplanation()
    {
        _settings.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isGstRegistered: false));

        var result = await _sut.GenerateAsync(new ReportFilterValues());

        Assert.Empty(result.Sections);
        Assert.Contains("not registered for GST", result.SubTitle);
    }

    // --- Known-numbers fixture ---

    [Fact]
    public async Task GenerateAsync_Registered_ComputesBasLabels_FromFixtureLedger()
    {
        _settings.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isGstRegistered: true));

        _gl.GetByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>
            {
                // Taxable sale: net $100 credited to Income (GST $10 goes to the 2310 clearing account separately)
                MakeLine(IncomeAccountId, credit: 100m, gstCode: GstCode.Gst),
                // GST-free sale: net $50 credited to Income, no GST component
                MakeLine(IncomeAccountId, credit: 50m, gstCode: GstCode.GstFree),
                // Taxable purchase: net $80 debited to Expense (GST $8 goes to the 2320 clearing account separately)
                MakeLine(ExpenseAccountId, debit: 80m, gstCode: GstCode.Gst)
            });

        _gl.GetAccountMovementsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, (decimal Debits, decimal Credits)>
            {
                [SystemAccounts.GstCollectedId] = (0m, 10m),
                [SystemAccounts.GstPaidId] = (8m, 0m)
            });

        var filters = new ReportFilterValues();
        filters.Set("dateFrom", $"{From:yyyy-MM-dd}");
        filters.Set("dateTo", $"{To:yyyy-MM-dd}");

        var result = await _sut.GenerateAsync(filters);

        var rows = result.Sections.Single().Rows;
        Assert.Equal("160.00", rows.Single(r => r.Cells[0] == "G1").Cells[2]);   // 150 coded income + 10 GST on sales
        Assert.Equal("50.00", rows.Single(r => r.Cells[0] == "G3").Cells[2]);    // GST-free sales only
        Assert.Equal("88.00", rows.Single(r => r.Cells[0] == "G11").Cells[2]);   // 80 coded expense + 8 raw GST paid debits
        Assert.Equal("10.00", rows.Single(r => r.Cells[0] == "1A").Cells[2]);
        Assert.Equal("8.00", rows.Single(r => r.Cells[0] == "1B").Cells[2]);

        Assert.NotNull(result.GrandTotal);
        Assert.Equal("2.00", result.GrandTotal!.Cells[2]);
        Assert.Contains("payable", result.GrandTotal.Cells[1]);
    }

    [Fact]
    public async Task GenerateAsync_Registered_NetGstNegative_ShowsRefundable()
    {
        _settings.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isGstRegistered: true));

        _gl.GetByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>());

        _gl.GetAccountMovementsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, (decimal Debits, decimal Credits)>
            {
                [SystemAccounts.GstCollectedId] = (0m, 5m),
                [SystemAccounts.GstPaidId] = (20m, 0m)
            });

        var filters = new ReportFilterValues();
        filters.Set("dateFrom", $"{From:yyyy-MM-dd}");
        filters.Set("dateTo", $"{To:yyyy-MM-dd}");

        var result = await _sut.GenerateAsync(filters);

        Assert.Equal("15.00", result.GrandTotal!.Cells[2]);
        Assert.Contains("refundable", result.GrandTotal.Cells[1]);
    }

    // --- Helpers ---

    private static Settings MakeSettings(bool isGstRegistered) => new()
    {
        Id = Guid.NewGuid(), OrganizationName = "Test Choir",
        AnnualFee = 50m, AttendanceFee = 10m,
        MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
        MinimumMemberAge = 0, SchemaVersion = "1.1.0",
        FinancialYearStartMonth = 7, IsGstRegistered = isGstRegistered,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Account MakeAccount(Guid id, AccountType type, string number) => new()
    {
        Id = id, Name = number, Type = type, AccountNumber = number,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Transaction MakeLine(Guid accountId, decimal debit = 0m, decimal credit = 0m, GstCode? gstCode = null) => new()
    {
        Id = Guid.NewGuid(), AccountId = accountId, Date = From.AddDays(1),
        DebitAmount = debit, CreditAmount = credit, GLAccount = "0000",
        GstCode = gstCode, CreatedAt = DateTime.UtcNow
    };
}
