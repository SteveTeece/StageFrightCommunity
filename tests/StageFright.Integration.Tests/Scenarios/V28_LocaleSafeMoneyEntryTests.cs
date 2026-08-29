using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// V28 acceptance — US2 AC-1…AC-3 (spec 028): with the device region set to French then German,
/// known amounts entered into the manual journal and the opening-balance form are stored in the
/// ledger exactly to the cent — identical to the same input under en-AU — and no digit is read
/// as a thousands separator. Drives the real <see cref="GeneralJournalService"/> /
/// <see cref="OpeningBalanceService"/> over in-memory SQLite with the full migration set, feeding
/// each amount through <see cref="MoneyInput.Parse"/> exactly as the razor code-behind does after
/// T034 / T035.
/// </summary>
[Collection("MoneyFormatterState")]
public sealed class V28_LocaleSafeMoneyEntryTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;

    private static readonly Guid IncomeId = Guid.NewGuid();
    private static readonly DateTime PostDate = new(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();   // seeds the 7 system accounts incl. Cash 1100 + Opening Balance Equity 3100

        _db.Accounts.Add(new Account
        {
            Id = IncomeId, Name = "Membership Dues", Type = AccountType.Income, AccountNumber = "4000",
            SortOrder = 0, IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        CultureInfo.CurrentCulture = _originalCulture;
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // AC-1 + AC-3: a French / German locale journal line stores the exact typed amount, with no
    // digit read as a group separator.
    [Theory]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    public async Task Should_StoreTheExactLedgerValue_When_JournalEnteredUnderACommaDecimalRegion(string cultureName)
    {
        var storedDebit = await PostJournalUnderCultureAsync(cultureName, "1234.50");
        Assert.Equal(1234.50m, storedDebit);
        Assert.NotEqual(123450m, storedDebit);

        var storedHalf = await PostJournalUnderCultureAsync(cultureName, "1.50");
        Assert.Equal(1.50m, storedHalf);
        Assert.NotEqual(150m, storedHalf);
    }

    // AC-2: the same input produces the same stored ledger value in every region.
    [Fact]
    public async Task Should_StoreTheSameLedgerValue_ForTheSameInput_InEveryRegion()
    {
        var au = await PostJournalUnderCultureAsync("en-AU", "1234.50");
        var fr = await PostJournalUnderCultureAsync("fr-FR", "1234.50");
        var de = await PostJournalUnderCultureAsync("de-DE", "1234.50");

        Assert.Equal(1234.50m, au);
        Assert.Equal(au, fr);
        Assert.Equal(au, de);
    }

    // AC-1 (opening balances): a German-locale opening balance stores exactly to the cent.
    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public async Task Should_StoreTheExactOpeningBalance_When_EnteredUnderACommaDecimalRegion(string cultureName)
    {
        var storedDebit = await PostOpeningBalanceUnderCultureAsync(cultureName, "1234.50");
        Assert.Equal(1234.50m, storedDebit);
        Assert.NotEqual(123450m, storedDebit);
    }

    // --- Drivers -------------------------------------------------------------------

    /// <summary>Posts DR Cash / CR Membership Dues for <paramref name="typed"/> parsed under <paramref name="cultureName"/>; returns the stored Cash debit.</summary>
    private async Task<decimal> PostJournalUnderCultureAsync(string cultureName, string typed)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        var amount = MoneyInput.Parse(typed);   // exactly what JournalEntryPage.ParseAmount does after T034

        var service = BuildJournalService();
        await service.RecordJournalAsync(new RecordJournalRequest
        {
            Date = PostDate,
            Description = $"Locale entry {cultureName} {typed}",
            Lines =
            [
                new JournalLine { AccountId = SystemAccounts.CashId, DebitAmount = amount, CreditAmount = 0m },
                new JournalLine { AccountId = IncomeId, DebitAmount = 0m, CreditAmount = amount },
            ]
        }, TestContext.Current.CancellationToken);

        var line = await _db.Transactions.AsNoTracking()
            .Where(t => t.AccountId == SystemAccounts.CashId && t.Description == $"Locale entry {cultureName} {typed}")
            .OrderByDescending(t => t.CreatedAt)
            .FirstAsync(TestContext.Current.CancellationToken);
        return line.DebitAmount;
    }

    /// <summary>Posts a single Cash opening balance for <paramref name="typed"/> parsed under <paramref name="cultureName"/>; returns the stored Cash debit.</summary>
    private async Task<decimal> PostOpeningBalanceUnderCultureAsync(string cultureName, string typed)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        var amount = MoneyInput.Parse(typed);   // exactly what OpeningBalanceEntryForm.SetAmount does after T035

        var service = BuildOpeningBalanceService();
        await service.RecordOpeningBalancesAsync(new RecordOpeningBalancesRequest
        {
            AsAtDate = PostDate,
            Entries = [new OpeningBalanceEntry { AccountId = SystemAccounts.CashId, Amount = amount }]
        }, TestContext.Current.CancellationToken);

        var line = await _db.Transactions.AsNoTracking()
            .Where(t => t.AccountId == SystemAccounts.CashId && t.DebitAmount != 0m)
            .OrderByDescending(t => t.CreatedAt)
            .FirstAsync(TestContext.Current.CancellationToken);
        return line.DebitAmount;
    }

    private GeneralJournalService BuildJournalService()
    {
        var audit = new AuditTrailService(new AuditTrailRepository(_db), NullLogger<AuditTrailService>.Instance);
        return new GeneralJournalService(
            new AccountRepository(_db), new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db))), new JournalEntryRepository(_db),
            audit, new UnitOfWork(_db), RealLocalizer.Instance);
    }

    private OpeningBalanceService BuildOpeningBalanceService()
    {
        var audit = new AuditTrailService(new AuditTrailRepository(_db), NullLogger<AuditTrailService>.Instance);
        return new OpeningBalanceService(
            new AccountRepository(_db), new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db))), new JournalEntryRepository(_db),
            audit, new UnitOfWork(_db), RealLocalizer.Instance);
    }
}
