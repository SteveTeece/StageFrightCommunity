using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;
using StageFright.UI.Pages.Finance;

namespace StageFright.UI.Tests.Pages.Finance;

/// <summary>
/// DepositPage wired to the real BankingService on in-memory SQLite (spec 033 US3): proves the page-to-service
/// path end to end, which <see cref="DepositPageTests"/> (mocked service) cannot.
/// </summary>
public sealed class DepositPageSqliteTests : LocalizedTestContext
{
    private static readonly Guid IncomeId = Guid.NewGuid();
    private static readonly Guid BankId = Guid.NewGuid();
    private static readonly DateTime Day1 = new(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);

    private readonly StageFrightDbContext _db;

    public DepositPageSqliteTests()
    {
        // Created by the container, so disposed with the test context.
        Services.AddSingleton(_ =>
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            return connection;
        });
        Services.AddSingleton(sp => new StageFrightDbContext(
            new DbContextOptionsBuilder<StageFrightDbContext>().UseSqlite(sp.GetRequiredService<SqliteConnection>()).Options));
        Services.AddScoped<IBankingService>(sp => BuildBanking(sp.GetRequiredService<StageFrightDbContext>(), sp.GetRequiredService<ILocalizer>()));

        _db = Services.GetRequiredService<StageFrightDbContext>();
        _db.Database.Migrate();
    }

    [Fact]
    public async Task Should_PostDepositAndStopOfferingCheque_When_SavedThroughRealBankingService()
    {
        await SeedAsync();
        var banked = await ChequeIdAsync("CHQ 1");
        var kept = await ChequeIdAsync("CHQ 2");
        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = Render<DepositPage>();
        cut.WaitForElement($"#deposit-item-{banked}");

        cut.Find($"#deposit-item-{banked}").Change(true);
        cut.Find("#depositCash").Input("30");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.EndsWith("/finance/banking", nav.Uri));
        Assert.Equal(80m, await BalanceAsync(BankId));
        Assert.Equal(120m, await BalanceAsync(SystemAccounts.UndepositedFundsId));
        Assert.Equal(45m, await BalanceAsync(SystemAccounts.CashId));

        var again = Render<DepositPage>();
        again.WaitForElement($"#deposit-item-{kept}");
        Assert.Empty(again.FindAll($"#deposit-item-{banked}"));
    }

    [Fact]
    public async Task Should_ShowCashAvailableAndPostNothing_When_CashAboveAvailableSavedThroughRealBankingService()
    {
        await SeedAsync();
        var cut = Render<DepositPage>();
        cut.WaitForElement("#depositCash");

        cut.Find("#depositCash").Input("500");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains(MoneyFormatter.Format(75m), cut.Find("[role=alert]").TextContent));
        Assert.Equal(0, await _db.BankDeposits.CountAsync(Xunit.TestContext.Current.CancellationToken));
        Assert.Equal(75m, await BalanceAsync(SystemAccounts.CashId));
    }

    // Two cheques (50 and 120) awaiting deposit and 75 cash in Petty Cash, all received on Day1.
    private async Task SeedAsync()
    {
        var now = DateTime.UtcNow;
        _db.Accounts.AddRange(
            new Account { Id = IncomeId, Name = "Raffle Income", Type = AccountType.Income, AccountNumber = "4000", SortOrder = 0, CreatedAt = now, UpdatedAt = now },
            new Account { Id = BankId, Name = "Community Bank", Type = AccountType.Asset, AccountNumber = "1110", IsBankAccount = true, SortOrder = 0, CreatedAt = now, UpdatedAt = now });
        _db.Settings.Add(new StageFright.Core.Entities.Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Test Choir", AnnualFee = 50m, AttendanceFee = 10m,
            MembershipRenewalMonth = 1, MaxAgeRangeYears = 150, MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            FinancialYearStartMonth = 7, FinancialYearStartDay = 1, CurrencyCode = "AUD", IsTaxApplicable = false,
            CreatedAt = now, UpdatedAt = now
        });
        await _db.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);

        var income = new IncomeEntryService(new AccountRepository(_db), Gl(_db), new JournalEntryRepository(_db), new SettingsRepository(_db),
            Audit(_db), new UnitOfWork(_db), Substitute.For<ILocalizer>(), new ExternalOrganisationRepository(_db), new ReceivedItemRepository(_db));
        await income.RecordIncomeAsync(new RecordIncomeRequest { Date = Day1, Amount = 50m, AccountId = IncomeId, PaymentMethod = PaymentMethod.Check, Reference = "CHQ 1" }, Xunit.TestContext.Current.CancellationToken);
        await income.RecordIncomeAsync(new RecordIncomeRequest { Date = Day1, Amount = 120m, AccountId = IncomeId, PaymentMethod = PaymentMethod.Check, Reference = "CHQ 2" }, Xunit.TestContext.Current.CancellationToken);
        await income.RecordIncomeAsync(new RecordIncomeRequest { Date = Day1, Amount = 75m, AccountId = IncomeId, PaymentMethod = PaymentMethod.Cash }, Xunit.TestContext.Current.CancellationToken);
    }

    private Task<Guid> ChequeIdAsync(string reference) =>
        _db.ReceivedItems.Where(i => i.Reference == reference).Select(i => i.Id).SingleAsync(Xunit.TestContext.Current.CancellationToken);

    // Debits minus credits on an account.
    private Task<decimal> BalanceAsync(Guid accountId) =>
        _db.Transactions.Where(t => t.AccountId == accountId).SumAsync(t => t.DebitAmount - t.CreditAmount, Xunit.TestContext.Current.CancellationToken);

    private static GLRepository Gl(StageFrightDbContext db) => new(db, new ClosedPeriodGuard(new SettingsRepository(db)));

    private static AuditTrailService Audit(StageFrightDbContext db) =>
        new(new AuditTrailRepository(db), NullLogger<AuditTrailService>.Instance);

    private static BankingService BuildBanking(StageFrightDbContext db, ILocalizer localizer) =>
        new(new AccountRepository(db), Gl(db), new JournalEntryRepository(db), new ReceivedItemRepository(db), new BankDepositRepository(db),
            new BankReconciliationRepository(db), new ClosedPeriodGuard(new SettingsRepository(db)), Audit(db), new UnitOfWork(db), localizer);
}
