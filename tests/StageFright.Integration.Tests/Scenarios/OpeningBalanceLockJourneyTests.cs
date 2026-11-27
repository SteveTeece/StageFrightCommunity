using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Issue #398 — opening balances are locked once posted. Drives the real
/// <see cref="OpeningBalanceService"/> over in-memory SQLite with the full migration set: a second
/// posting is refused and leaves the ledger untouched, and the posted balances read back as entered.
/// </summary>
public sealed class OpeningBalanceLockJourneyTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid LoanId = Guid.NewGuid();
    private static readonly DateTime AsAt = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();   // seeds the system accounts incl. Petty Cash 1100 + Opening Balance Equity 3100

        _db.Accounts.Add(new Account
        {
            Id = LoanId, Name = "Committee Loan", Type = AccountType.Liability, AccountNumber = "2000",
            SortOrder = 0, IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Should_RefuseSecondPostingAndLeaveLedgerUnchanged_When_OpeningBalancesAlreadyPosted_Integration()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = BuildService();
        await service.RecordOpeningBalancesAsync(Request(SystemAccounts.CashId, 500m), ct);
        var linesBefore = await _db.Transactions.CountAsync(ct);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.RecordOpeningBalancesAsync(Request(SystemAccounts.CashId, 250m), ct));

        Assert.Equal(linesBefore, await _db.Transactions.CountAsync(ct));
        Assert.Equal(1, await _db.JournalEntries.CountAsync(j => j.Type == JournalEntryType.OpeningBalance, ct));
        var cashDebits = await _db.Transactions
            .Where(t => t.AccountId == SystemAccounts.CashId)
            .SumAsync(t => t.DebitAmount, ct);
        Assert.Equal(500m, cashDebits);
    }

    [Fact]
    public async Task Should_ReadBackPostedBalancesAndPlug_When_OpeningBalancesRecorded_Integration()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = BuildService();
        Assert.Null((await service.GetRecordedOpeningBalancesAsync(ct)).AsAtDate);

        await service.RecordOpeningBalancesAsync(new RecordOpeningBalancesRequest
        {
            AsAtDate = AsAt,
            Entries =
            [
                new OpeningBalanceEntry { AccountId = SystemAccounts.CashId, Amount = 500m },
                new OpeningBalanceEntry { AccountId = LoanId, Amount = 200m }
            ]
        }, ct);

        var recorded = await service.GetRecordedOpeningBalancesAsync(ct);

        Assert.Equal(AsAt, recorded.AsAtDate);
        Assert.Equal(500m, recorded.Lines.Single(l => l.Account.Id == SystemAccounts.CashId).Amount);
        Assert.Equal(200m, recorded.Lines.Single(l => l.Account.Id == LoanId).Amount);
        Assert.All(recorded.Lines.Where(l => l.Account.Id != SystemAccounts.CashId && l.Account.Id != LoanId),
            l => Assert.Equal(0m, l.Amount));
        Assert.DoesNotContain(recorded.Lines, l => l.Account.Id == SystemAccounts.OpeningBalanceEquityId);
        Assert.Equal(300m, recorded.Plug);   // 500 DR asset − 200 CR liability = 300 CR to Opening Balance Equity
    }

    [Fact]
    public async Task Should_RecordZeroAndLock_When_NoOpeningBalanceEntered_Integration()
    {
        // Setup finished with nothing entered (issue #398): recorded as zero, with no ledger lines.
        var ct = TestContext.Current.CancellationToken;
        var service = BuildService();

        await service.RecordOpeningBalancesAsync(new RecordOpeningBalancesRequest { AsAtDate = AsAt }, ct);

        Assert.Equal(1, await _db.JournalEntries.CountAsync(j => j.Type == JournalEntryType.OpeningBalance, ct));
        Assert.Equal(0, await _db.Transactions.CountAsync(ct));
        var recorded = await service.GetRecordedOpeningBalancesAsync(ct);
        Assert.Equal(AsAt, recorded.AsAtDate);
        Assert.All(recorded.Lines, l => Assert.Equal(0m, l.Amount));
        await Assert.ThrowsAsync<ValidationException>(
            () => service.RecordOpeningBalancesAsync(Request(SystemAccounts.CashId, 250m), ct));
    }

    private static RecordOpeningBalancesRequest Request(Guid accountId, decimal amount) => new()
    {
        AsAtDate = AsAt,
        Entries = [new OpeningBalanceEntry { AccountId = accountId, Amount = amount }]
    };

    private OpeningBalanceService BuildService()
    {
        var audit = new AuditTrailService(new AuditTrailRepository(_db), NullLogger<AuditTrailService>.Instance);
        return new OpeningBalanceService(
            new AccountRepository(_db), new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db))), new JournalEntryRepository(_db),
            new ReceivedItemRepository(_db), audit, new UnitOfWork(_db), RealLocalizer.Instance);
    }
}
