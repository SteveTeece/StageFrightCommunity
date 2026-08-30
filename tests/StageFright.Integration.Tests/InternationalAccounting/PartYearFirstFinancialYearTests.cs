using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;

namespace StageFright.Integration.Tests.InternationalAccounting;

/// <summary>
/// Spec 028 FR-022 / issue #353: an organisation founded after its financial-year anchor gets a
/// first financial year shorter than twelve months, running from the inception date to the day
/// before the next anchor, and every FY-preset report labels that default period a part-year.
/// A null inception date (every pre-existing dataset) and an inception date on the anchor are
/// unchanged. Real in-memory SQLite, full migrations.
/// </summary>
public sealed class PartYearFirstFinancialYearTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid IncomeId = Guid.NewGuid();
    private static readonly Guid ExpenseId = Guid.NewGuid();

    private const int FyMonth = 7;
    private const int FyDay = 1;

    // Neutral (en-AU) fragment of Reports_Common_PartYearSubtitle.
    private const string PartYearMarker = "(part-year — first financial year)";

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
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    private async Task SeedSettingsAsync(DateTime? inceptionDate, bool taxApplicable = false)
    {
        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Founded Mid-Year Inc",
            AnnualFee = 50m, AttendanceFee = 10m, MembershipRenewalMonth = 1,
            MaxAgeRangeYears = 150, MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            FinancialYearStartMonth = FyMonth, FinancialYearStartDay = FyDay,
            CurrencyCode = "AUD", InceptionDate = inceptionDate,
            IsTaxApplicable = taxApplicable, TaxRate = taxApplicable ? 10m : null,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private SettingsRepository SettingsRepo() => new(_db);
    private AccountRepository AccountRepo() => new(_db);
    private GLRepository Gl() => new(_db, new ClosedPeriodGuard(SettingsRepo()));

    private IncomeStatementReportProvider Income() => new(Gl(), AccountRepo(), SettingsRepo(), RealLocalizer.Instance);
    private TrialBalanceReportProvider Trial() => new(Gl(), AccountRepo(), SettingsRepo(), RealLocalizer.Instance);
    private BalanceSheetReportProvider Balance() => new(Gl(), AccountRepo(), SettingsRepo(), RealLocalizer.Instance);
    private TaxSummaryReportProvider Tax() => new(Gl(), AccountRepo(), SettingsRepo(), RealLocalizer.Instance);

    // --- Inception after the anchor → first period opens on the inception date + part-year label ---

    [Fact]
    public async Task Should_BoundAndLabelTheFirstPeriodAsPartYear_When_InceptionFallsAfterTheAnchor()
    {
        var (fyStart, _) = FinancialYearCalculator.GetRange(DateTime.UtcNow, FyMonth, FyDay);
        var inception = fyStart.AddDays(20);   // within the current financial year, after the anchor
        await SeedSettingsAsync(inception);

        var income = await Income().GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);
        Assert.Contains(inception.ToString("d MMMM yyyy"), income.SubTitle);
        Assert.Contains(PartYearMarker, income.SubTitle);
        // The period opens on the inception date, not the anchor.
        Assert.StartsWith(inception.ToString("d MMMM yyyy"), income.SubTitle);

        var trial = await Trial().GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);
        Assert.Contains(inception.ToString("d MMMM yyyy"), trial.SubTitle);
        Assert.Contains(PartYearMarker, trial.SubTitle);

        var balance = await Balance().GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);
        Assert.Contains(PartYearMarker, balance.SubTitle);
    }

    [Fact]
    public async Task Should_LabelTheTaxSummaryQuarterAsPartYear_When_InceptionFallsInsideTheCurrentQuarter()
    {
        // Replicate the provider's current-quarter maths so the inception date lands inside it.
        var (fyFrom, _) = FinancialYearCalculator.GetRange(DateTime.UtcNow, FyMonth, FyDay);
        var monthsElapsed = ((DateTime.UtcNow.Year - fyFrom.Year) * 12) + DateTime.UtcNow.Month - fyFrom.Month;
        var quarterFrom = fyFrom.AddMonths((monthsElapsed / 3) * 3);
        var inception = quarterFrom.AddDays(15);
        await SeedSettingsAsync(inception, taxApplicable: true);

        var tax = await Tax().GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        Assert.Contains(inception.ToString("d MMMM yyyy"), tax.SubTitle);
        Assert.Contains(PartYearMarker, tax.SubTitle);
    }

    // --- Inception on the anchor / null inception → full twelve-month first year, no label ---

    [Fact]
    public async Task Should_NotLabelAPartYear_When_InceptionIsOnTheAnchor()
    {
        var (fyStart, _) = FinancialYearCalculator.GetRange(DateTime.UtcNow, FyMonth, FyDay);
        await SeedSettingsAsync(fyStart);

        var income = await Income().GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        Assert.Contains(fyStart.ToString("d MMMM yyyy"), income.SubTitle);
        Assert.DoesNotContain(PartYearMarker, income.SubTitle);
    }

    [Fact]
    public async Task Should_LeaveEveryReportUnchanged_When_ThereIsNoInceptionDate()
    {
        var (fyStart, _) = FinancialYearCalculator.GetRange(DateTime.UtcNow, FyMonth, FyDay);
        await SeedSettingsAsync(inceptionDate: null, taxApplicable: true);

        var income = await Income().GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);
        var trial = await Trial().GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);
        var balance = await Balance().GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);
        var tax = await Tax().GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        Assert.Contains(fyStart.ToString("d MMMM yyyy"), income.SubTitle);
        foreach (var subtitle in new[] { income.SubTitle, trial.SubTitle, balance.SubTitle, tax.SubTitle })
            Assert.DoesNotContain(PartYearMarker, subtitle);
    }

    [Fact]
    public async Task Should_DefaultInceptionDateToNull_On_AFreshlyMigratedDataset()
    {
        await SeedSettingsAsync(inceptionDate: null);

        var persisted = await _db.Settings.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);

        Assert.Null(persisted.InceptionDate);
    }
}
