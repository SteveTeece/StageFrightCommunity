using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Settings;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for the V1 first-run setup scenario.
/// Verifies the end-to-end flow from empty database to fully initialised settings.
/// Uses a real SQLite in-memory database with full EF migrations applied.
/// </summary>
public sealed class V1_FirstRunSetupTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task BeforeSetup_IsSetupComplete_ReturnsFalse()
    {
        var svc = BuildSetupService();
        Assert.False(await svc.IsSetupCompleteAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AfterSetup_IsSetupComplete_ReturnsTrue()
    {
        var svc = BuildSetupService();
        await svc.InitializeAsync(ValidRequest(), TestContext.Current.CancellationToken);

        Assert.True(await svc.IsSetupCompleteAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AfterSetup_SettingsPersisted_WithCorrectValues()
    {
        var svc = BuildSetupService();
        var request = new SetupRequest("Springfield Choir", 75m, 5m, 9, false, null, null, null, Core.Enums.Theme.Dark);
        await svc.InitializeAsync(request, TestContext.Current.CancellationToken);

        var settings = await new SettingsRepository(_db).GetAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(settings);
        Assert.Equal("Springfield Choir", settings!.OrganizationName);
        Assert.Equal(75m, settings.AnnualFee);
        Assert.Equal(5m, settings.AttendanceFee);
        Assert.Equal(9, settings.MembershipRenewalMonth);
    }

    [Fact]
    public async Task AfterSetup_SystemAccountsExist_WithCorrectGLAccounts()
    {
        var svc = BuildSetupService();
        await svc.InitializeAsync(ValidRequest(), TestContext.Current.CancellationToken);

        var accounts = await new AccountRepository(_db).GetAllAsync(TestContext.Current.CancellationToken);
        Assert.Contains(accounts, c => c.AccountNumber == "1100" && c.Name == "Cash on Hand" && c.IsSystem);
        Assert.Contains(accounts, c => c.AccountNumber == "1200" && c.IsSystem);
        Assert.Contains(accounts, c => c.AccountNumber == "6999" && c.IsSystem);
    }

    [Fact]
    public async Task AfterSetup_ZeroFeeRecords_Exist()
    {
        var svc = BuildSetupService();
        await svc.InitializeAsync(ValidRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(0, await _db.Fees.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializingTwice_Throws_ValidationException()
    {
        var svc = BuildSetupService();
        await svc.InitializeAsync(ValidRequest(), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ValidationException>(
            () => svc.InitializeAsync(ValidRequest(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WithEmptyOrgName_Throws_ValidationException()
    {
        var svc = BuildSetupService();

        await Assert.ThrowsAsync<ValidationException>(
            () => svc.InitializeAsync(ValidRequest() with { OrganizationName = "" }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WithQueuedAccounts_CreatesEachAccount()
    {
        var svc = BuildSetupService();
        var queued = new[]
        {
            new QueuedAccountRequest(Guid.NewGuid(), "Petty Cash", Core.Enums.AccountType.Asset, true),
            new QueuedAccountRequest(Guid.NewGuid(), "Grant Income", Core.Enums.AccountType.Income, false)
        };
        await svc.InitializeAsync(ValidRequest() with { QueuedAccounts = queued }, TestContext.Current.CancellationToken);

        var accounts = await new AccountRepository(_db).GetAllAsync(TestContext.Current.CancellationToken);
        Assert.Contains(accounts, a => a.Name == "Petty Cash" && a.Type == Core.Enums.AccountType.Asset && a.IsBankAccount && !a.IsSystem);
        Assert.Contains(accounts, a => a.Name == "Grant Income" && a.Type == Core.Enums.AccountType.Income && !a.IsBankAccount && !a.IsSystem);
    }

    [Fact]
    public async Task InitializingTwice_WithQueuedAccounts_DoesNotCreateDuplicateAccount()
    {
        // The second call fails at the "already completed" guard before it ever reaches
        // queued-account creation, so the account is created exactly once (data-model.md's
        // account transition — nothing here re-runs on a rejected re-initialization).
        var svc = BuildSetupService();
        var queued = new[] { new QueuedAccountRequest(Guid.NewGuid(), "Petty Cash", Core.Enums.AccountType.Asset, true) };
        await svc.InitializeAsync(ValidRequest() with { QueuedAccounts = queued }, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ValidationException>(
            () => svc.InitializeAsync(ValidRequest() with { QueuedAccounts = queued }, TestContext.Current.CancellationToken));

        var accounts = await new AccountRepository(_db).GetAllAsync(TestContext.Current.CancellationToken);
        Assert.Single(accounts, a => a.Name == "Petty Cash");
    }

    [Fact]
    public async Task InitializeAsync_WithQueuedOpeningBalances_PostsOneBalancedOpeningBalanceEntry()
    {
        var svc = BuildSetupService();
        var cashAccount = (await new AccountRepository(_db).GetAllAsync(TestContext.Current.CancellationToken))
            .Single(a => a.AccountNumber == "1100");
        var asAtDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await svc.InitializeAsync(ValidRequest() with
        {
            QueuedOpeningBalances = new[] { new OpeningBalanceEntry { AccountId = cashAccount.Id, Amount = 500m } },
            OpeningBalanceAsAtDate = asAtDate
        }, TestContext.Current.CancellationToken);

        var journalEntry = Assert.Single(_db.JournalEntries, j => j.Type == JournalEntryType.OpeningBalance);
        Assert.Equal(asAtDate, journalEntry.Date);

        var lines = await _db.Transactions.Where(t => t.JournalEntryId == journalEntry.Id).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(lines, l => l.AccountId == cashAccount.Id && l.DebitAmount == 500m);
        Assert.Equal(lines.Sum(l => l.DebitAmount), lines.Sum(l => l.CreditAmount));
    }

    [Fact]
    public async Task InitializeAsync_WithQueuedAccountAndOpeningBalance_ResolvesClientIdToRealAccountId()
    {
        var svc = BuildSetupService();
        var clientId = Guid.NewGuid();
        var asAtDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await svc.InitializeAsync(ValidRequest() with
        {
            QueuedAccounts = new[] { new QueuedAccountRequest(clientId, "Petty Cash", Core.Enums.AccountType.Asset, true) },
            QueuedOpeningBalances = new[] { new OpeningBalanceEntry { AccountId = clientId, Amount = 200m } },
            OpeningBalanceAsAtDate = asAtDate
        }, TestContext.Current.CancellationToken);

        var pettyCash = (await new AccountRepository(_db).GetAllAsync(TestContext.Current.CancellationToken)).Single(a => a.Name == "Petty Cash");
        Assert.NotEqual(clientId, pettyCash.Id); // resolved to a real, freshly assigned Account.Id

        var journalEntry = Assert.Single(_db.JournalEntries, j => j.Type == JournalEntryType.OpeningBalance);
        var lines = await _db.Transactions.Where(t => t.JournalEntryId == journalEntry.Id).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(lines, l => l.AccountId == pettyCash.Id && l.DebitAmount == 200m);
        Assert.DoesNotContain(lines, l => l.AccountId == clientId);
        Assert.Equal(lines.Sum(l => l.DebitAmount), lines.Sum(l => l.CreditAmount));
    }

    private SetupService BuildSetupService()
    {
        var settingsRepo = new SettingsRepository(_db);
        var accountRepo = new AccountRepository(_db);
        var eventTypeRepo = new EventTypeRepository(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditService = new Core.Modules.AuditTrail.AuditTrailService(
            auditRepo, NullLogger<Core.Modules.AuditTrail.AuditTrailService>.Instance);
        var officeHolderTypeRepo = new CommitteeOfficeHolderTypeRepository(_db);
        var officeHolderTypeService = new Core.Modules.Members.CommitteeOfficeHolderTypeService(officeHolderTypeRepo, auditService, RealLocalizer.Instance);
        var glAssignment = new AccountNumberAssignmentService(accountRepo);
        var reconciliationRepo = new BankReconciliationRepository(_db);
        var accountService = new AccountService(accountRepo, glAssignment, auditService, reconciliationRepo, RealLocalizer.Instance);
        var glRepo = new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db)));
        var journalRepo = new JournalEntryRepository(_db);
        var unitOfWork = new UnitOfWork(_db);
        var openingBalanceService = new OpeningBalanceService(accountRepo, glRepo, journalRepo, auditService, unitOfWork, RealLocalizer.Instance);
        return new SetupService(settingsRepo, accountRepo, eventTypeRepo, officeHolderTypeService, accountService, openingBalanceService, auditService, RealLocalizer.Instance);
    }

    private static SetupRequest ValidRequest() =>
        new("Test Organisation", 60m, 4m, 1, false, null, null, null, Core.Enums.Theme.Dark);
}
