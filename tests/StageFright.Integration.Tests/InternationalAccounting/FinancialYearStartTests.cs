using Microsoft.EntityFrameworkCore;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Settings;
using StageFright.Data;
using StageFright.Data.Repositories;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;

namespace StageFright.Integration.Tests.InternationalAccounting;

/// <summary>
/// Spec 028 US7 (FR-019 / FR-020 / FR-021, SC-001): first-run setup can choose a financial year
/// that starts on a day other than the first of the month, that choice is persisted, and every
/// FY-preset report then bounds its default period on the configured (month, day) anchor. An
/// existing Australian dataset (July, day 1) is unchanged. Real in-memory SQLite, full migrations.
/// </summary>
public sealed class FinancialYearStartTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid IncomeId = Guid.NewGuid();
    private static readonly Guid ExpenseId = Guid.NewGuid();

    // A real non-first-of-month fiscal calendar: 6 April.
    private const int FyMonth = 4;
    private const int FyDay = 6;

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
        await _db.SaveChangesAsync();
        // Settings is seeded per test (setup test must run with none present).
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    private async Task SeedSettingsAsync(int startMonth, int startDay, bool taxApplicable = false)
    {
        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Test Choir",
            AnnualFee = 50m, AttendanceFee = 10m, MembershipRenewalMonth = 1,
            MaxAgeRangeYears = 150, MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            FinancialYearStartMonth = startMonth, FinancialYearStartDay = startDay,
            CurrencyCode = "AUD",
            IsTaxApplicable = taxApplicable, TaxRate = taxApplicable ? 10m : null,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private SettingsRepository SettingsRepo() => new(_db);
    private AccountRepository AccountRepo() => new(_db);
    private GLRepository Gl() => new(_db, new ClosedPeriodGuard(SettingsRepo()));

    // --- Setup persists a non-first-of-month start (FR-019 / FR-020) ---

    [Fact]
    public async Task Should_PersistANonFirstOfMonthFinancialYearStart_When_ChosenAtSetup()
    {
        var svc = new SetupService(
            SettingsRepo(),
            Substitute.For<IAccountRepository>(),
            Substitute.For<IEventTypeRepository>(),
            Substitute.For<ICommitteeOfficeHolderTypeService>(),
            Substitute.For<IAccountService>(),
            Substitute.For<IOpeningBalanceService>(),
            Substitute.For<IAuditTrailService>(),
            RealLocalizer.Instance);

        await svc.InitializeAsync(new SetupRequest(
            OrganizationName: "Founded Mid-Year Inc",
            AnnualFee: 60m, AttendanceFee: 8m, MembershipRenewalMonth: 1,
            IsTaxApplicable: false, TaxRate: null, AnnualFeeTaxCode: null, AttendanceFeeTaxCode: null,
            Theme: Theme.Dark,
            FinancialYearStartMonth: FyMonth, FinancialYearStartDay: FyDay), TestContext.Current.CancellationToken);

        var persisted = await _db.Settings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(FyMonth, persisted.FinancialYearStartMonth);
        Assert.Equal(FyDay, persisted.FinancialYearStartDay);
    }

    // --- FY-preset reports honour month AND day (FR-021) ---

    [Fact]
    public async Task Should_BoundIncomeStatementAndTrialBalance_OnTheConfiguredAnchorDay()
    {
        await SeedSettingsAsync(FyMonth, FyDay);
        var (fyStart, _) = FinancialYearCalculator.GetRange(DateTime.UtcNow, FyMonth, FyDay);
        var firstOfMonth = new DateTime(fyStart.Year, FyMonth, 1);

        var income = await new IncomeStatementReportProvider(Gl(), AccountRepo(), SettingsRepo(), RealLocalizer.Instance)
            .GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);
        Assert.Contains(fyStart.ToString("d MMMM yyyy"), income.SubTitle);
        Assert.DoesNotContain(firstOfMonth.ToString("d MMMM yyyy"), income.SubTitle);

        var trial = await new TrialBalanceReportProvider(Gl(), AccountRepo(), SettingsRepo(), RealLocalizer.Instance)
            .GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);
        Assert.Contains(fyStart.ToString("d MMMM yyyy"), trial.SubTitle);
    }

    [Fact]
    public async Task Should_BoundBalanceSheetAsAt_TheConfiguredYearEndDay()
    {
        await SeedSettingsAsync(FyMonth, FyDay);
        var (_, fyEnd) = FinancialYearCalculator.GetRange(DateTime.UtcNow, FyMonth, FyDay);

        var balance = await new BalanceSheetReportProvider(Gl(), AccountRepo(), SettingsRepo(), RealLocalizer.Instance)
            .GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        Assert.Contains(fyEnd.ToString("d MMMM yyyy"), balance.SubTitle);
    }

    [Fact]
    public async Task Should_BoundTaxSummaryQuarter_OnTheConfiguredAnchorDay()
    {
        await SeedSettingsAsync(FyMonth, FyDay, taxApplicable: true);

        // Replicate the provider's current-quarter maths so the assertion pins the (month, day) anchor.
        var (fyFrom, _) = FinancialYearCalculator.GetRange(DateTime.UtcNow, FyMonth, FyDay);
        var monthsElapsed = ((DateTime.UtcNow.Year - fyFrom.Year) * 12) + DateTime.UtcNow.Month - fyFrom.Month;
        var expectedQuarterFrom = fyFrom.AddMonths((monthsElapsed / 3) * 3);

        var tax = await new TaxSummaryReportProvider(Gl(), AccountRepo(), SettingsRepo(), RealLocalizer.Instance)
            .GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        Assert.Equal(FyDay, expectedQuarterFrom.Day);
        Assert.Contains(expectedQuarterFrom.ToString("d MMMM yyyy"), tax.SubTitle);
    }

    // --- Existing AU dataset (July, day 1) is unchanged (SC-001 / US7 AC-3) ---

    [Fact]
    public async Task Should_LeaveAnAustralianDatasetsRangesUnchanged_When_StartIsJulyFirst()
    {
        await SeedSettingsAsync(7, 1);
        var (fyStart, _) = FinancialYearCalculator.GetRange(DateTime.UtcNow, 7, 1);

        var income = await new IncomeStatementReportProvider(Gl(), AccountRepo(), SettingsRepo(), RealLocalizer.Instance)
            .GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        Assert.Equal(1, fyStart.Day);
        Assert.Equal(7, fyStart.Month);
        Assert.Contains(fyStart.ToString("d MMMM yyyy"), income.SubTitle);
    }
}
