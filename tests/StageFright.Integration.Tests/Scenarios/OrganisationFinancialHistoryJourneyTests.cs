using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Organisations;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Spec 032 US3 journey (SC-003): a $500 expense paid to "Town Hall" and a $200 income entry
/// received from it give a history totalling exactly $500 paid and $200 received, and an unlinked
/// expense appears in no organisation's history. Real services and repositories on a migrated
/// SQLite in-memory database.
/// </summary>
public sealed class OrganisationFinancialHistoryJourneyTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    private static readonly Guid SavingsAccountId = Guid.NewGuid();
    private static readonly Guid VenueHireAccountId = Guid.NewGuid();
    private static readonly Guid GrantIncomeAccountId = Guid.NewGuid();
    private static readonly Guid TownHallId = Guid.NewGuid();
    private static readonly Guid AcmeId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        _db.Accounts.AddRange(
            NewAccount(SavingsAccountId, "Savings", AccountType.Asset, "1110", isBank: true),
            NewAccount(VenueHireAccountId, "Venue Hire", AccountType.Expense, "6000"),
            NewAccount(GrantIncomeAccountId, "Grants", AccountType.Income, "4000"));
        _db.ExternalOrganisations.AddRange(NewOrganisation(TownHallId, "Town Hall"), NewOrganisation(AcmeId, "Acme Supplies"));

        await _db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Should_TotalExactlyPaidAndReceived_When_ExpenseAndIncomeLinkedToOrganisation_Integration()
    {
        var ct = TestContext.Current.CancellationToken;
        var expenseDate = DateTime.UtcNow.Date.AddDays(-2);
        var incomeDate = DateTime.UtcNow.Date.AddDays(-1);

        await BuildExpenseService().RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = expenseDate, Amount = 500m, BankAccountId = SavingsAccountId,
            ExpenseAccountId = VenueHireAccountId, Payee = "Town Hall",
            ExternalOrganisationId = TownHallId
        }, ct);

        await BuildIncomeService().RecordIncomeAsync(new RecordIncomeRequest
        {
            Date = incomeDate, Amount = 200m, AccountId = GrantIncomeAccountId,
            DepositAccountId = SavingsAccountId, Description = "Community grant",
            ExternalOrganisationId = TownHallId
        }, ct);

        await BuildExpenseService().RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = incomeDate, Amount = 75m, BankAccountId = SavingsAccountId,
            ExpenseAccountId = VenueHireAccountId, Payee = "Town Hall"
        }, ct);

        var history = await BuildHistoryService().GetHistoryAsync(TownHallId, ct);

        Assert.Equal(500m, history.TotalPaid);
        Assert.Equal(200m, history.TotalReceived);
        Assert.Equal(2, history.Entries.Count);
        Assert.Equal(JournalEntryType.Income, history.Entries[0].Type);
        Assert.Equal("Grants", history.Entries[0].AccountName);
        Assert.Equal(JournalEntryType.ExpensePayment, history.Entries[1].Type);
        Assert.Equal("Venue Hire", history.Entries[1].AccountName);
    }

    [Fact]
    public async Task Should_AppearInNoHistory_When_ExpenseRecordedWithoutOrganisation_Integration()
    {
        var ct = TestContext.Current.CancellationToken;

        await BuildExpenseService().RecordExpenseAsync(new RecordExpenseRequest
        {
            Date = DateTime.UtcNow.Date, Amount = 60m, BankAccountId = SavingsAccountId,
            ExpenseAccountId = VenueHireAccountId, Payee = "Town Hall"
        }, ct);

        var unlinked = await _db.JournalEntries.SingleAsync(ct);
        Assert.Null(unlinked.ExternalOrganisationId);

        var historyService = BuildHistoryService();
        Assert.Empty((await historyService.GetHistoryAsync(TownHallId, ct)).Entries);
        Assert.Empty((await historyService.GetHistoryAsync(AcmeId, ct)).Entries);
    }

    // --- Helpers ---

    private ExpensePaymentService BuildExpenseService() =>
        new(new AccountRepository(_db), new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db))),
            new JournalEntryRepository(_db), new SettingsRepository(_db), BuildAuditService(), new UnitOfWork(_db),
            RealLocalizer.Instance, new ExternalOrganisationRepository(_db));

    private IncomeEntryService BuildIncomeService() =>
        new(new AccountRepository(_db), new GLRepository(_db, new ClosedPeriodGuard(new SettingsRepository(_db))),
            new JournalEntryRepository(_db), new SettingsRepository(_db), BuildAuditService(), new UnitOfWork(_db),
            RealLocalizer.Instance, new ExternalOrganisationRepository(_db));

    private OrganisationHistoryService BuildHistoryService() => new(new JournalEntryRepository(_db));

    private static AuditTrailService BuildAuditService()
    {
        var auditRepo = NSubstitute.Substitute.For<StageFright.Core.Contracts.IAuditTrailRepository>();
        return new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
    }

    private static Account NewAccount(Guid id, string name, AccountType type, string number, bool isBank = false) => new()
    {
        Id = id, Name = name, Type = type, AccountNumber = number, IsBankAccount = isBank,
        SortOrder = 0, IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static ExternalOrganisation NewOrganisation(Guid id, string name) => new()
    {
        Id = id, Name = name, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
