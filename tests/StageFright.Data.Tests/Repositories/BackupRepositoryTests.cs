using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Data.Repositories;
using StageFright.Data.Tests.Infrastructure;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for <see cref="BackupRepository"/>'s handling of the external-organisation
/// register (spec 032, FR-015, T069). The snapshot must keep archived organisations and removed
/// contacts, and a restore must write organisations before the contacts and journal entries that
/// point at them, so SQLite's foreign-key checks pass.
/// </summary>
public class BackupRepositoryTests : IDisposable
{
    private static readonly DateTime Created = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Deleted = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly DbContextFactory _source = new();
    private readonly DbContextFactory _target = new();

    [Fact]
    public async Task Should_IncludeArchivedOrganisationsAndRemovedContacts_When_SnapshotRead_Integration()
    {
        var ct = TestContext.Current.CancellationToken;
        var register = await SeedRegisterAsync(ct);

        using var db = _source.CreateContext();
        var snapshot = await new BackupRepository(db).GetFullSnapshotAsync(ct);

        Assert.Equal(
            new[] { register.Active.Id, register.Archived.Id }.Order(),
            snapshot.ExternalOrganisations.Select(o => o.Id).Order());
        Assert.Equal(
            new[] { register.Contact.Id, register.RemovedContact.Id }.Order(),
            snapshot.ExternalOrganisationContacts.Select(c => c.Id).Order());
        Assert.Equal(register.Active.Id, snapshot.JournalEntries.Single(j => j.Id == register.Linked.Id).ExternalOrganisationId);
    }

    [Fact]
    public async Task Should_RestoreOrganisationsContactsAndLinks_When_UpsertedIntoEmptyDatabase_Integration()
    {
        var ct = TestContext.Current.CancellationToken;
        var register = await SeedRegisterAsync(ct);

        using (var sourceDb = _source.CreateContext())
        using (var targetDb = _target.CreateContext())
        {
            var snapshot = await new BackupRepository(sourceDb).GetFullSnapshotAsync(ct);
            await new BackupRepository(targetDb).UpsertSnapshotAsync(snapshot, ct);
        }

        using var check = _target.CreateContext();
        var orgs = await check.ExternalOrganisations.IgnoreQueryFilters().ToListAsync(ct);
        Assert.Equal(2, orgs.Count);
        Assert.True(orgs.Single(o => o.Id == register.Archived.Id).IsDeleted);

        var contacts = await check.ExternalOrganisationContacts.IgnoreQueryFilters().ToListAsync(ct);
        Assert.Equal(2, contacts.Count);
        Assert.True(contacts.Single(c => c.Id == register.RemovedContact.Id).IsDeleted);
        Assert.All(contacts, c => Assert.Equal(register.Active.Id, c.ExternalOrganisationId));

        var linked = await check.JournalEntries.SingleAsync(j => j.Id == register.Linked.Id, ct);
        Assert.Equal(register.Active.Id, linked.ExternalOrganisationId);
    }

    [Fact]
    public async Task Should_OverwriteChangedOrganisation_When_UpsertedOverExistingRow_Integration()
    {
        var ct = TestContext.Current.CancellationToken;
        var register = await SeedRegisterAsync(ct);

        using (var db = _source.CreateContext())
        {
            var snapshot = await new BackupRepository(db).GetFullSnapshotAsync(ct);
            snapshot.ExternalOrganisations.Single(o => o.Id == register.Active.Id).Name = "Renamed Hall";
            await new BackupRepository(db).UpsertSnapshotAsync(snapshot, ct);
        }

        using var check = _source.CreateContext();
        Assert.Equal(2, await check.ExternalOrganisations.IgnoreQueryFilters().CountAsync(ct));
        var org = await check.ExternalOrganisations.SingleAsync(o => o.Id == register.Active.Id, ct);
        Assert.Equal("Renamed Hall", org.Name);
    }

    private async Task<SeededRegister> SeedRegisterAsync(CancellationToken ct)
    {
        var active = new ExternalOrganisation
        {
            Id = Guid.NewGuid(), Name = "Town Hall", Address = "1 Main St", Email = "hall@example.org",
            CreatedAt = Created, UpdatedAt = Created
        };
        var archived = new ExternalOrganisation
        {
            Id = Guid.NewGuid(), Name = "Old Printer", IsDeleted = true, DeletedAt = Deleted, DeletedBy = "treasurer",
            CreatedAt = Created, UpdatedAt = Deleted
        };
        var contact = new ExternalOrganisationContact
        {
            Id = Guid.NewGuid(), ExternalOrganisationId = active.Id, Name = "Pat Lee", Role = "Bookings",
            CreatedAt = Created, UpdatedAt = Created
        };
        var removedContact = new ExternalOrganisationContact
        {
            Id = Guid.NewGuid(), ExternalOrganisationId = active.Id, Name = "Sam Roe",
            IsDeleted = true, DeletedAt = Deleted, DeletedBy = "treasurer",
            CreatedAt = Created, UpdatedAt = Deleted
        };
        var linked = new JournalEntry
        {
            Id = Guid.NewGuid(), Type = JournalEntryType.ExpensePayment, Date = Created,
            Description = "Hall hire", CreatedAt = Created, ExternalOrganisationId = active.Id
        };

        using var db = _source.CreateContext();
        db.ExternalOrganisations.AddRange(active, archived);
        db.ExternalOrganisationContacts.AddRange(contact, removedContact);
        db.JournalEntries.Add(linked);
        await db.SaveChangesAsync(ct);

        return new SeededRegister(active, archived, contact, removedContact, linked);
    }

    // --- spec 033: Petty Cash rename for files older than backup 1.4.0 ---

    [Fact]
    public async Task Should_RenameCashAndOwnPettyCash_When_RenameCashAccountToPettyCashCalled_Integration()
    {
        var ct = TestContext.Current.CancellationToken;
        var own = new Account
        {
            Id = Guid.NewGuid(), Name = "PETTY CASH", Type = AccountType.Asset, AccountNumber = "1150",
            CreatedAt = Created, UpdatedAt = Created
        };
        var unrelated = new Account
        {
            Id = Guid.NewGuid(), Name = "Petty Cash Tin", Type = AccountType.Asset, AccountNumber = "1160",
            CreatedAt = Created, UpdatedAt = Created
        };
        using (var db = _target.CreateContext())
        {
            var cash = await db.Accounts.SingleAsync(a => a.Id == SystemAccounts.CashId, ct);
            cash.Name = "Cash on Hand";
            db.Accounts.AddRange(own, unrelated);
            await db.SaveChangesAsync(ct);

            await new BackupRepository(db).RenameCashAccountToPettyCashAsync(ct);
        }

        using var check = _target.CreateContext();
        var accounts = await check.Accounts.IgnoreQueryFilters().ToDictionaryAsync(a => a.Id, ct);
        Assert.Equal("Petty Cash", accounts[SystemAccounts.CashId].Name);
        Assert.Equal(SystemAccounts.CashNumber, accounts[SystemAccounts.CashId].AccountNumber);
        Assert.Equal("Petty Cash (own)", accounts[own.Id].Name);
        Assert.Equal("Petty Cash Tin", accounts[unrelated.Id].Name);
    }

    [Fact]
    public async Task Should_LeaveAccountsUnchanged_When_CashAlreadyNamedPettyCash_Integration()
    {
        var ct = TestContext.Current.CancellationToken;
        DateTime before;
        using (var db = _target.CreateContext())
        {
            before = (await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == SystemAccounts.CashId, ct)).UpdatedAt;
            await new BackupRepository(db).RenameCashAccountToPettyCashAsync(ct);
        }

        using var check = _target.CreateContext();
        var cash = await check.Accounts.SingleAsync(a => a.Id == SystemAccounts.CashId, ct);
        Assert.Equal("Petty Cash", cash.Name);
        Assert.Equal(before, cash.UpdatedAt);
    }

    [Fact]
    public async Task Should_ThrowDataAccessException_When_RenameFails_Integration()
    {
        var db = _target.CreateContext();
        var repo = new BackupRepository(db);
        await db.DisposeAsync();

        await Assert.ThrowsAsync<DataAccessException>(
            () => repo.RenameCashAccountToPettyCashAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_RoundTripBankingRecords_When_SnapshotUpsertedIntoEmptyDatabase_Integration()
    {
        var ct = TestContext.Current.CancellationToken;
        var bank = new Account
        {
            Id = Guid.NewGuid(), Name = "Cheque Account", Type = AccountType.Asset, AccountNumber = "1010",
            IsBankAccount = true, CreatedAt = Created, UpdatedAt = Created
        };
        var incomeJe = new JournalEntry
        {
            Id = Guid.NewGuid(), Type = JournalEntryType.Income, Date = Created, Description = "Grant", CreatedAt = Created
        };
        var depositJe = new JournalEntry
        {
            Id = Guid.NewGuid(), Type = JournalEntryType.BankDeposit, Date = Created.AddDays(1), Description = "Deposit", CreatedAt = Created
        };
        var item = new ReceivedItem
        {
            Id = Guid.NewGuid(), Date = Created, Amount = 500m, Method = PaymentMethod.Check,
            Reference = "000123", JournalEntryId = incomeJe.Id, CreatedAt = Created
        };
        var deposit = new BankDeposit
        {
            Id = Guid.NewGuid(), Date = depositJe.Date, BankAccountId = bank.Id, CashAmount = 0m,
            ChequeTotal = 500m, JournalEntryId = depositJe.Id, CreatedAt = Created
        };
        var line = new BankDepositLine
        {
            Id = Guid.NewGuid(), BankDepositId = deposit.Id, ReceivedItemId = item.Id, CreatedAt = Created
        };
        using (var db = _source.CreateContext())
        {
            db.Accounts.Add(bank);
            db.JournalEntries.AddRange(incomeJe, depositJe);
            db.ReceivedItems.Add(item);
            db.BankDeposits.Add(deposit);
            db.BankDepositLines.Add(line);
            await db.SaveChangesAsync(ct);
        }

        using (var sourceDb = _source.CreateContext())
        using (var targetDb = _target.CreateContext())
        {
            var snapshot = await new BackupRepository(sourceDb).GetFullSnapshotAsync(ct);
            Assert.Single(snapshot.ReceivedItems);
            Assert.Single(snapshot.BankDeposits);
            Assert.Single(snapshot.BankDepositLines);
            await new BackupRepository(targetDb).UpsertSnapshotAsync(snapshot, ct);
        }

        using var check = _target.CreateContext();
        Assert.Equal(item.Id, (await check.ReceivedItems.SingleAsync(ct)).Id);
        Assert.Equal(deposit.Id, (await check.BankDeposits.SingleAsync(ct)).Id);
        var restoredLine = await check.BankDepositLines.SingleAsync(ct);
        Assert.Equal(deposit.Id, restoredLine.BankDepositId);
        Assert.Equal(item.Id, restoredLine.ReceivedItemId);
    }

    public void Dispose()
    {
        _source.Dispose();
        _target.Dispose();
    }

    private sealed record SeededRegister(
        ExternalOrganisation Active,
        ExternalOrganisation Archived,
        ExternalOrganisationContact Contact,
        ExternalOrganisationContact RemovedContact,
        JournalEntry Linked);
}
