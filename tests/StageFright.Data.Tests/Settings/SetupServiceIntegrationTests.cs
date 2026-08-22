using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Members;
using StageFright.Core.Modules.Settings;
using StageFright.Data;
using StageFright.Data.Repositories;
using StageFright.Data.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace StageFright.Data.Tests.Setup;

/// <summary>
/// Integration tests for SetupService using an in-memory SQLite database.
/// Verifies Settings persistence, system account seeding, and zero Fee records post-setup.
/// </summary>
public class SetupServiceIntegrationTests : IDisposable
{
    private readonly DbContextFactory _factory = new();

    [Fact]
    public async Task InitializeAsync_PersistsSettingsWithCorrectValues()
    {
        using var db = _factory.CreateContext();
        var settingsRepo = new SettingsRepository(db);
        var accountRepo = new AccountRepository(db);
        var eventTypeRepo = new EventTypeRepository(db);
        var auditRepo = new AuditTrailRepository(db);
        var auditService = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var officeHolderTypeRepo = new CommitteeOfficeHolderTypeRepository(db);
        var officeHolderTypeService = new CommitteeOfficeHolderTypeService(officeHolderTypeRepo, auditService);
        var glAssignment = new AccountNumberAssignmentService(accountRepo);
        var reconciliationRepo = new BankReconciliationRepository(db);
        var accountService = new AccountService(accountRepo, glAssignment, auditService, reconciliationRepo);
        var glRepo = new GLRepository(db);
        var journalRepo = new JournalEntryRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var openingBalanceService = new OpeningBalanceService(accountRepo, glRepo, journalRepo, auditService, unitOfWork);
        var svc = new SetupService(settingsRepo, accountRepo, eventTypeRepo, officeHolderTypeService, accountService, openingBalanceService, auditService);

        var request = new SetupRequest("My Choir", 80m, 6m, 3, false, null, null, null, Theme.Dark);
        await svc.InitializeAsync(request, TestContext.Current.CancellationToken);

        var settings = await settingsRepo.GetAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(settings);
        Assert.Equal("My Choir", settings!.OrganizationName);
        Assert.Equal(80m, settings.AnnualFee);
        Assert.Equal(6m, settings.AttendanceFee);
        Assert.Equal(3, settings.MembershipRenewalMonth);
    }

    [Fact]
    public async Task InitializeAsync_SeedsSystemAccounts()
    {
        using var db = _factory.CreateContext();
        var settingsRepo = new SettingsRepository(db);
        var accountRepo = new AccountRepository(db);
        var eventTypeRepo = new EventTypeRepository(db);
        var auditRepo = new AuditTrailRepository(db);
        var auditService = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var officeHolderTypeRepo = new CommitteeOfficeHolderTypeRepository(db);
        var officeHolderTypeService = new CommitteeOfficeHolderTypeService(officeHolderTypeRepo, auditService);
        var glAssignment = new AccountNumberAssignmentService(accountRepo);
        var reconciliationRepo = new BankReconciliationRepository(db);
        var accountService = new AccountService(accountRepo, glAssignment, auditService, reconciliationRepo);
        var glRepo = new GLRepository(db);
        var journalRepo = new JournalEntryRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var openingBalanceService = new OpeningBalanceService(accountRepo, glRepo, journalRepo, auditService, unitOfWork);
        var svc = new SetupService(settingsRepo, accountRepo, eventTypeRepo, officeHolderTypeService, accountService, openingBalanceService, auditService);

        await svc.InitializeAsync(new SetupRequest("Org", 50m, 5m, 1, false, null, null, null, Theme.Dark), TestContext.Current.CancellationToken);

        var all = await accountRepo.GetAllAsync(TestContext.Current.CancellationToken);
        Assert.Contains(all, c => c.AccountNumber == "1100" && c.Name == "Cash on Hand" && c.IsSystem);
        Assert.Contains(all, c => c.AccountNumber == "1200" && c.IsSystem);
        Assert.Contains(all, c => c.AccountNumber == "6999" && c.IsSystem);
    }

    [Fact]
    public async Task InitializeAsync_CreatesZeroFeeRecords()
    {
        using var db = _factory.CreateContext();
        var settingsRepo = new SettingsRepository(db);
        var accountRepo = new AccountRepository(db);
        var eventTypeRepo = new EventTypeRepository(db);
        var auditRepo = new AuditTrailRepository(db);
        var auditService = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var officeHolderTypeRepo = new CommitteeOfficeHolderTypeRepository(db);
        var officeHolderTypeService = new CommitteeOfficeHolderTypeService(officeHolderTypeRepo, auditService);
        var glAssignment = new AccountNumberAssignmentService(accountRepo);
        var reconciliationRepo = new BankReconciliationRepository(db);
        var accountService = new AccountService(accountRepo, glAssignment, auditService, reconciliationRepo);
        var glRepo = new GLRepository(db);
        var journalRepo = new JournalEntryRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var openingBalanceService = new OpeningBalanceService(accountRepo, glRepo, journalRepo, auditService, unitOfWork);
        var svc = new SetupService(settingsRepo, accountRepo, eventTypeRepo, officeHolderTypeService, accountService, openingBalanceService, auditService);

        await svc.InitializeAsync(new SetupRequest("Org", 50m, 5m, 1, false, null, null, null, Theme.Dark), TestContext.Current.CancellationToken);

        var feeCount = await db.Fees.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, feeCount);
    }

    [Fact]
    public async Task IsSetupCompleteAsync_ReturnsFalse_OnEmptyDatabase()
    {
        using var db = _factory.CreateContext();
        var settingsRepo = new SettingsRepository(db);
        var accountRepo = new AccountRepository(db);
        var eventTypeRepo = new EventTypeRepository(db);
        var auditRepo = new AuditTrailRepository(db);
        var auditService = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var officeHolderTypeRepo = new CommitteeOfficeHolderTypeRepository(db);
        var officeHolderTypeService = new CommitteeOfficeHolderTypeService(officeHolderTypeRepo, auditService);
        var glAssignment = new AccountNumberAssignmentService(accountRepo);
        var reconciliationRepo = new BankReconciliationRepository(db);
        var accountService = new AccountService(accountRepo, glAssignment, auditService, reconciliationRepo);
        var glRepo = new GLRepository(db);
        var journalRepo = new JournalEntryRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var openingBalanceService = new OpeningBalanceService(accountRepo, glRepo, journalRepo, auditService, unitOfWork);
        var svc = new SetupService(settingsRepo, accountRepo, eventTypeRepo, officeHolderTypeService, accountService, openingBalanceService, auditService);

        Assert.False(await svc.IsSetupCompleteAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_PersistsDefaultAuditRetentionYears()
    {
        using var db = _factory.CreateContext();
        var settingsRepo = new SettingsRepository(db);
        var accountRepo = new AccountRepository(db);
        var eventTypeRepo = new EventTypeRepository(db);
        var auditRepo = new AuditTrailRepository(db);
        var auditService = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var officeHolderTypeRepo = new CommitteeOfficeHolderTypeRepository(db);
        var officeHolderTypeService = new CommitteeOfficeHolderTypeService(officeHolderTypeRepo, auditService);
        var glAssignment = new AccountNumberAssignmentService(accountRepo);
        var reconciliationRepo = new BankReconciliationRepository(db);
        var accountService = new AccountService(accountRepo, glAssignment, auditService, reconciliationRepo);
        var glRepo = new GLRepository(db);
        var journalRepo = new JournalEntryRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var openingBalanceService = new OpeningBalanceService(accountRepo, glRepo, journalRepo, auditService, unitOfWork);
        var svc = new SetupService(settingsRepo, accountRepo, eventTypeRepo, officeHolderTypeService, accountService, openingBalanceService, auditService);

        await svc.InitializeAsync(new SetupRequest("Org", 50m, 5m, 1, false, null, null, null, Theme.Dark), TestContext.Current.CancellationToken);

        var settings = await settingsRepo.GetAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, settings!.AuditRetentionYears);
    }

    [Fact]
    public async Task InitializeAsync_PersistsCustomAuditRetentionYears()
    {
        using var db = _factory.CreateContext();
        var settingsRepo = new SettingsRepository(db);
        var accountRepo = new AccountRepository(db);
        var eventTypeRepo = new EventTypeRepository(db);
        var auditRepo = new AuditTrailRepository(db);
        var auditService = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var officeHolderTypeRepo = new CommitteeOfficeHolderTypeRepository(db);
        var officeHolderTypeService = new CommitteeOfficeHolderTypeService(officeHolderTypeRepo, auditService);
        var glAssignment = new AccountNumberAssignmentService(accountRepo);
        var reconciliationRepo = new BankReconciliationRepository(db);
        var accountService = new AccountService(accountRepo, glAssignment, auditService, reconciliationRepo);
        var glRepo = new GLRepository(db);
        var journalRepo = new JournalEntryRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var openingBalanceService = new OpeningBalanceService(accountRepo, glRepo, journalRepo, auditService, unitOfWork);
        var svc = new SetupService(settingsRepo, accountRepo, eventTypeRepo, officeHolderTypeService, accountService, openingBalanceService, auditService);

        var request = new SetupRequest("Org", 50m, 5m, 1, false, null, null, null, Theme.Dark)
        {
            AuditRetentionYears = 7
        };
        await svc.InitializeAsync(request, TestContext.Current.CancellationToken);

        var settings = await settingsRepo.GetAsync(TestContext.Current.CancellationToken);
        Assert.Equal(7, settings!.AuditRetentionYears);
    }

    public void Dispose() => _factory.Dispose();
}
