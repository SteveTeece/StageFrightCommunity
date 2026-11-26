using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
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
/// Verifies Settings persistence, system account seeding, zero Fee records post-setup, and the
/// Undeposited Funds opening balance queued in the wizard (spec 033).
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
        var officeHolderTypeService = new CommitteeOfficeHolderTypeService(officeHolderTypeRepo, auditService, RealLocalizer.Instance);
        var glAssignment = new AccountNumberAssignmentService(accountRepo);
        var reconciliationRepo = new BankReconciliationRepository(db);
        var accountService = new AccountService(accountRepo, glAssignment, auditService, reconciliationRepo, RealLocalizer.Instance);
        var glRepo = new GLRepository(db, new ClosedPeriodGuard(new SettingsRepository(db)));
        var journalRepo = new JournalEntryRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var openingBalanceService = new OpeningBalanceService(accountRepo, glRepo, journalRepo, new ReceivedItemRepository(db), auditService, unitOfWork, RealLocalizer.Instance);
        var svc = new SetupService(settingsRepo, accountRepo, eventTypeRepo, officeHolderTypeService, accountService, openingBalanceService, auditService, RealLocalizer.Instance);

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
        var officeHolderTypeService = new CommitteeOfficeHolderTypeService(officeHolderTypeRepo, auditService, RealLocalizer.Instance);
        var glAssignment = new AccountNumberAssignmentService(accountRepo);
        var reconciliationRepo = new BankReconciliationRepository(db);
        var accountService = new AccountService(accountRepo, glAssignment, auditService, reconciliationRepo, RealLocalizer.Instance);
        var glRepo = new GLRepository(db, new ClosedPeriodGuard(new SettingsRepository(db)));
        var journalRepo = new JournalEntryRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var openingBalanceService = new OpeningBalanceService(accountRepo, glRepo, journalRepo, new ReceivedItemRepository(db), auditService, unitOfWork, RealLocalizer.Instance);
        var svc = new SetupService(settingsRepo, accountRepo, eventTypeRepo, officeHolderTypeService, accountService, openingBalanceService, auditService, RealLocalizer.Instance);

        await svc.InitializeAsync(new SetupRequest("Org", 50m, 5m, 1, false, null, null, null, Theme.Dark), TestContext.Current.CancellationToken);

        var all = await accountRepo.GetAllAsync(TestContext.Current.CancellationToken);
        Assert.Contains(all, c => c.AccountNumber == "1100" && c.Name == "Petty Cash" && c.IsSystem);
        Assert.Contains(all, c => c.AccountNumber == "1105" && c.Name == "Undeposited Funds" && c.IsSystem && !c.IsBankAccount);
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
        var officeHolderTypeService = new CommitteeOfficeHolderTypeService(officeHolderTypeRepo, auditService, RealLocalizer.Instance);
        var glAssignment = new AccountNumberAssignmentService(accountRepo);
        var reconciliationRepo = new BankReconciliationRepository(db);
        var accountService = new AccountService(accountRepo, glAssignment, auditService, reconciliationRepo, RealLocalizer.Instance);
        var glRepo = new GLRepository(db, new ClosedPeriodGuard(new SettingsRepository(db)));
        var journalRepo = new JournalEntryRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var openingBalanceService = new OpeningBalanceService(accountRepo, glRepo, journalRepo, new ReceivedItemRepository(db), auditService, unitOfWork, RealLocalizer.Instance);
        var svc = new SetupService(settingsRepo, accountRepo, eventTypeRepo, officeHolderTypeService, accountService, openingBalanceService, auditService, RealLocalizer.Instance);

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
        var officeHolderTypeService = new CommitteeOfficeHolderTypeService(officeHolderTypeRepo, auditService, RealLocalizer.Instance);
        var glAssignment = new AccountNumberAssignmentService(accountRepo);
        var reconciliationRepo = new BankReconciliationRepository(db);
        var accountService = new AccountService(accountRepo, glAssignment, auditService, reconciliationRepo, RealLocalizer.Instance);
        var glRepo = new GLRepository(db, new ClosedPeriodGuard(new SettingsRepository(db)));
        var journalRepo = new JournalEntryRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var openingBalanceService = new OpeningBalanceService(accountRepo, glRepo, journalRepo, new ReceivedItemRepository(db), auditService, unitOfWork, RealLocalizer.Instance);
        var svc = new SetupService(settingsRepo, accountRepo, eventTypeRepo, officeHolderTypeService, accountService, openingBalanceService, auditService, RealLocalizer.Instance);

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
        var officeHolderTypeService = new CommitteeOfficeHolderTypeService(officeHolderTypeRepo, auditService, RealLocalizer.Instance);
        var glAssignment = new AccountNumberAssignmentService(accountRepo);
        var reconciliationRepo = new BankReconciliationRepository(db);
        var accountService = new AccountService(accountRepo, glAssignment, auditService, reconciliationRepo, RealLocalizer.Instance);
        var glRepo = new GLRepository(db, new ClosedPeriodGuard(new SettingsRepository(db)));
        var journalRepo = new JournalEntryRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var openingBalanceService = new OpeningBalanceService(accountRepo, glRepo, journalRepo, new ReceivedItemRepository(db), auditService, unitOfWork, RealLocalizer.Instance);
        var svc = new SetupService(settingsRepo, accountRepo, eventTypeRepo, officeHolderTypeService, accountService, openingBalanceService, auditService, RealLocalizer.Instance);

        await svc.InitializeAsync(new SetupRequest("Org", 50m, 5m, 1, false, null, null, null, Theme.Dark), TestContext.Current.CancellationToken);

        var settings = await settingsRepo.GetAsync(TestContext.Current.CancellationToken);
        // spec 028, US8 / FR-023: the default retention on a fresh dataset is now five years.
        Assert.Equal(5, settings!.AuditRetentionYears);
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
        var officeHolderTypeService = new CommitteeOfficeHolderTypeService(officeHolderTypeRepo, auditService, RealLocalizer.Instance);
        var glAssignment = new AccountNumberAssignmentService(accountRepo);
        var reconciliationRepo = new BankReconciliationRepository(db);
        var accountService = new AccountService(accountRepo, glAssignment, auditService, reconciliationRepo, RealLocalizer.Instance);
        var glRepo = new GLRepository(db, new ClosedPeriodGuard(new SettingsRepository(db)));
        var journalRepo = new JournalEntryRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var openingBalanceService = new OpeningBalanceService(accountRepo, glRepo, journalRepo, new ReceivedItemRepository(db), auditService, unitOfWork, RealLocalizer.Instance);
        var svc = new SetupService(settingsRepo, accountRepo, eventTypeRepo, officeHolderTypeService, accountService, openingBalanceService, auditService, RealLocalizer.Instance);

        var request = new SetupRequest("Org", 50m, 5m, 1, false, null, null, null, Theme.Dark)
        {
            AuditRetentionYears = 7
        };
        await svc.InitializeAsync(request, TestContext.Current.CancellationToken);

        var settings = await settingsRepo.GetAsync(TestContext.Current.CancellationToken);
        Assert.Equal(7, settings!.AuditRetentionYears);
    }

    [Fact]
    public async Task Should_ListOneOpeningBalanceItemAwaitingDeposit_When_SetupQueuesPositiveUndepositedFunds_Integration()
    {
        using var db = _factory.CreateContext();
        var svc = BuildSetupService(db);
        var asAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        await svc.InitializeAsync(MakeSetupRequest(asAt, 320m), TestContext.Current.CancellationToken);

        var awaiting = await new ReceivedItemRepository(db).GetAwaitingAsync(TestContext.Current.CancellationToken);
        var item = Assert.Single(awaiting);
        Assert.Equal(320m, item.Amount);
        Assert.Equal(PaymentMethod.Other, item.Method);
        Assert.Equal(asAt, item.Date);
        Assert.Null(item.PaymentId);
        Assert.NotNull(item.JournalEntry);
        Assert.Equal(JournalEntryType.OpeningBalance, item.JournalEntry!.Type);

        var undepositedFundsDebits = await db.Transactions
            .Where(t => t.AccountId == SystemAccounts.UndepositedFundsId)
            .SumAsync(t => t.DebitAmount - t.CreditAmount, TestContext.Current.CancellationToken);
        Assert.Equal(320m, undepositedFundsDebits);
    }

    [Fact]
    public async Task Should_RefuseAndPostNothing_When_SetupQueuesNegativeUndepositedFunds_Integration()
    {
        using var db = _factory.CreateContext();
        var svc = BuildSetupService(db);

        await Assert.ThrowsAsync<ValidationException>(() => svc.InitializeAsync(
            MakeSetupRequest(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), -40m),
            TestContext.Current.CancellationToken));

        Assert.Equal(0, await db.ReceivedItems.CountAsync(TestContext.Current.CancellationToken));
        Assert.False(await db.Transactions.AnyAsync(
            t => t.AccountId == SystemAccounts.UndepositedFundsId, TestContext.Current.CancellationToken));
    }

    private static SetupService BuildSetupService(StageFrightDbContext db)
    {
        var settingsRepo = new SettingsRepository(db);
        var accountRepo = new AccountRepository(db);
        var auditService = new AuditTrailService(new AuditTrailRepository(db), NullLogger<AuditTrailService>.Instance);
        var officeHolderTypeService = new CommitteeOfficeHolderTypeService(
            new CommitteeOfficeHolderTypeRepository(db), auditService, RealLocalizer.Instance);
        var accountService = new AccountService(
            accountRepo, new AccountNumberAssignmentService(accountRepo), auditService,
            new BankReconciliationRepository(db), RealLocalizer.Instance);
        var glRepo = new GLRepository(db, new ClosedPeriodGuard(new SettingsRepository(db)));
        var openingBalanceService = new OpeningBalanceService(
            accountRepo, glRepo, new JournalEntryRepository(db), new ReceivedItemRepository(db),
            auditService, new UnitOfWork(db), RealLocalizer.Instance);
        return new SetupService(
            settingsRepo, accountRepo, new EventTypeRepository(db), officeHolderTypeService,
            accountService, openingBalanceService, auditService, RealLocalizer.Instance);
    }

    private static SetupRequest MakeSetupRequest(DateTime asAt, decimal undepositedFunds) =>
        new("Org", 50m, 5m, 1, false, null, null, null, Theme.Dark)
        {
            QueuedOpeningBalances =
            [
                new OpeningBalanceEntry { AccountId = SystemAccounts.UndepositedFundsId, Amount = undepositedFunds },
                new OpeningBalanceEntry { AccountId = SystemAccounts.CashId, Amount = 100m }
            ],
            OpeningBalanceAsAtDate = asAt
        };

    public void Dispose() => _factory.Dispose();
}
