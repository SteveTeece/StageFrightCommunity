using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
        Assert.False(await svc.IsSetupCompleteAsync());
    }

    [Fact]
    public async Task AfterSetup_IsSetupComplete_ReturnsTrue()
    {
        var svc = BuildSetupService();
        await svc.InitializeAsync(ValidRequest());

        Assert.True(await svc.IsSetupCompleteAsync());
    }

    [Fact]
    public async Task AfterSetup_SettingsPersisted_WithCorrectValues()
    {
        var svc = BuildSetupService();
        var request = new SetupRequest("Springfield Choir", 75m, 5m, 9, false, null, null, null, Core.Enums.Theme.Dark);
        await svc.InitializeAsync(request);

        var settings = await new SettingsRepository(_db).GetAsync();
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
        await svc.InitializeAsync(ValidRequest());

        var accounts = await new AccountRepository(_db).GetAllAsync();
        Assert.Contains(accounts, c => c.AccountNumber == "1100" && c.Name == "Cash on Hand" && c.IsSystem);
        Assert.Contains(accounts, c => c.AccountNumber == "1200" && c.IsSystem);
        Assert.Contains(accounts, c => c.AccountNumber == "6999" && c.IsSystem);
    }

    [Fact]
    public async Task AfterSetup_ZeroFeeRecords_Exist()
    {
        var svc = BuildSetupService();
        await svc.InitializeAsync(ValidRequest());

        Assert.Equal(0, await _db.Fees.CountAsync());
    }

    [Fact]
    public async Task InitializingTwice_Throws_ValidationException()
    {
        var svc = BuildSetupService();
        await svc.InitializeAsync(ValidRequest());

        await Assert.ThrowsAsync<ValidationException>(
            () => svc.InitializeAsync(ValidRequest()));
    }

    [Fact]
    public async Task InitializeAsync_WithEmptyOrgName_Throws_ValidationException()
    {
        var svc = BuildSetupService();

        await Assert.ThrowsAsync<ValidationException>(
            () => svc.InitializeAsync(ValidRequest() with { OrganizationName = "" }));
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
        var officeHolderTypeService = new Core.Modules.Members.CommitteeOfficeHolderTypeService(officeHolderTypeRepo, auditService);
        var glAssignment = new AccountNumberAssignmentService(accountRepo);
        var reconciliationRepo = new BankReconciliationRepository(_db);
        var accountService = new AccountService(accountRepo, glAssignment, auditService, reconciliationRepo);
        var glRepo = new GLRepository(_db);
        var journalRepo = new JournalEntryRepository(_db);
        var unitOfWork = new UnitOfWork(_db);
        var openingBalanceService = new OpeningBalanceService(accountRepo, glRepo, journalRepo, auditService, unitOfWork);
        return new SetupService(settingsRepo, accountRepo, eventTypeRepo, officeHolderTypeService, accountService, openingBalanceService, auditService);
    }

    private static SetupRequest ValidRequest() =>
        new("Test Organisation", 60m, 4m, 1, false, null, null, null, Core.Enums.Theme.Dark);
}
