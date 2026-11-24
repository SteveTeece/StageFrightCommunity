using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Data.Repositories;
using StageFright.Data.Tests.Infrastructure;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for <see cref="JournalEntryRepository.GetByExternalOrganisationAsync"/> using
/// SQLite in-memory connections (spec 032 US3): only the requested organisation's entries come back,
/// with their lines and accounts loaded, newest first, and still when the organisation is archived.
/// </summary>
public class JournalEntryRepositoryTests : IDisposable
{
    private readonly DbContextFactory _factory = new();
    private static readonly Guid HallHireId = Guid.NewGuid();

    private static ExternalOrganisation NewOrganisation(string name, bool archived = false) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        IsDeleted = archived,
        DeletedAt = archived ? DateTime.UtcNow : null,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static JournalEntry NewExpense(Guid? organisationId, DateTime date, decimal amount, DateTime? createdAt = null)
    {
        var id = Guid.NewGuid();
        return new JournalEntry
        {
            Id = id,
            Type = JournalEntryType.ExpensePayment,
            Date = date,
            Description = $"Expense {amount}",
            CreatedAt = createdAt ?? DateTime.UtcNow,
            ExternalOrganisationId = organisationId,
            Transactions =
            [
                new Transaction
                {
                    Id = Guid.NewGuid(), Date = date, AccountId = HallHireId, DebitAmount = amount,
                    GLAccount = "6000", JournalEntryId = id, CreatedAt = DateTime.UtcNow
                },
                new Transaction
                {
                    Id = Guid.NewGuid(), Date = date, AccountId = SystemAccounts.CashId, CreditAmount = amount,
                    GLAccount = SystemAccounts.CashNumber, JournalEntryId = id, CreatedAt = DateTime.UtcNow
                }
            ]
        };
    }

    private async Task SeedAsync(IEnumerable<ExternalOrganisation> organisations, IEnumerable<JournalEntry> entries)
    {
        using var db = _factory.CreateContext();
        db.Accounts.Add(new Account
        {
            Id = HallHireId, Name = "Hall Hire", Type = AccountType.Expense, AccountNumber = "6000",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.ExternalOrganisations.AddRange(organisations);
        db.JournalEntries.AddRange(entries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_ReturnOnlyThatOrganisationsEntriesNewestFirst_When_Queried_Integration()
    {
        var townHall = NewOrganisation("Town Hall");
        var other = NewOrganisation("Acme Supplies");
        var older = NewExpense(townHall.Id, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), 100m);
        var newer = NewExpense(townHall.Id, new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc), 200m);
        var otherEntry = NewExpense(other.Id, new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc), 300m);
        var unlinked = NewExpense(null, new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), 400m);
        await SeedAsync([townHall, other], [older, newer, otherEntry, unlinked]);

        using var db = _factory.CreateContext();
        var result = await new JournalEntryRepository(db)
            .GetByExternalOrganisationAsync(townHall.Id, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { newer.Id, older.Id }, result.Select(j => j.Id));
    }

    [Fact]
    public async Task Should_LoadLinesAndTheirAccounts_When_Queried_Integration()
    {
        var townHall = NewOrganisation("Town Hall");
        var entry = NewExpense(townHall.Id, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), 100m);
        await SeedAsync([townHall], [entry]);

        using var db = _factory.CreateContext();
        var result = await new JournalEntryRepository(db)
            .GetByExternalOrganisationAsync(townHall.Id, TestContext.Current.CancellationToken);

        var loaded = Assert.Single(result);
        Assert.Equal(2, loaded.Transactions.Count);
        Assert.Contains(loaded.Transactions, t => t.Account.Name == "Hall Hire" && t.DebitAmount == 100m);
        Assert.Contains(loaded.Transactions, t => t.Account.IsBankAccount && t.CreditAmount == 100m);
    }

    [Fact]
    public async Task Should_OrderByCreatedAtDescending_When_EntriesShareADate_Integration()
    {
        var townHall = NewOrganisation("Town Hall");
        var date = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var first = NewExpense(townHall.Id, date, 10m, createdAt: new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc));
        var second = NewExpense(townHall.Id, date, 20m, createdAt: new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc));
        await SeedAsync([townHall], [first, second]);

        using var db = _factory.CreateContext();
        var result = await new JournalEntryRepository(db)
            .GetByExternalOrganisationAsync(townHall.Id, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { second.Id, first.Id }, result.Select(j => j.Id));
    }

    [Fact]
    public async Task Should_StillReturnEntries_When_OrganisationIsArchived_Integration()
    {
        var archived = NewOrganisation("Old Venue", archived: true);
        var entry = NewExpense(archived.Id, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), 100m);
        await SeedAsync([archived], [entry]);

        using var db = _factory.CreateContext();
        var result = await new JournalEntryRepository(db)
            .GetByExternalOrganisationAsync(archived.Id, TestContext.Current.CancellationToken);

        Assert.Equal(entry.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_OrganisationHasNoEntries_Integration()
    {
        await SeedAsync([], []);

        using var db = _factory.CreateContext();
        var result = await new JournalEntryRepository(db)
            .GetByExternalOrganisationAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Should_ThrowDataAccessException_When_DatabaseUnavailable_Integration()
    {
        var db = _factory.CreateContext();
        await db.DisposeAsync();

        await Assert.ThrowsAsync<StageFright.Core.Exceptions.DataAccessException>(() =>
            new JournalEntryRepository(db).GetByExternalOrganisationAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    public void Dispose() => _factory.Dispose();
}
