using StageFright.Core.Entities;
using StageFright.Data.Repositories;
using StageFright.Data.Tests.Infrastructure;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for <see cref="ExternalOrganisationRepository"/> using SQLite in-memory
/// connections (spec 032): archived organisations are hidden by default, the including-archived
/// lookup still finds them, every read's contact list excludes removed contacts, and the
/// active-name check ignores case, surrounding whitespace, archived names and the excluded id.
/// </summary>
public class ExternalOrganisationRepositoryTests : IDisposable
{
    private readonly DbContextFactory _factory = new();

    private static ExternalOrganisation NewOrganisation(string name, bool archived = false) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        IsDeleted = archived,
        DeletedAt = archived ? DateTime.UtcNow : null,
        DeletedBy = archived ? "coordinator" : null,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static ExternalOrganisationContact NewContact(Guid organisationId, string name, bool removed = false) => new()
    {
        Id = Guid.NewGuid(),
        ExternalOrganisationId = organisationId,
        Name = name,
        IsDeleted = removed,
        DeletedAt = removed ? DateTime.UtcNow : null,
        DeletedBy = removed ? "coordinator" : null,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private async Task SeedAsync(IEnumerable<ExternalOrganisation> organisations, IEnumerable<ExternalOrganisationContact>? contacts = null)
    {
        using var db = _factory.CreateContext();
        db.ExternalOrganisations.AddRange(organisations);
        if (contacts is not null)
            db.ExternalOrganisationContacts.AddRange(contacts);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // --- Archived organisations hidden by default ---

    [Fact]
    public async Task Should_ReturnOnlyActiveOrganisationsOrderedByName_When_GetAllAsyncCalled_Integration()
    {
        await SeedAsync(new[]
        {
            NewOrganisation("Town Hall"),
            NewOrganisation("Archived Venue", archived: true),
            NewOrganisation("Acme Supplies")
        });

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var result = await repo.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Acme Supplies", "Town Hall" }, result.Select(o => o.Name));
    }

    [Fact]
    public async Task Should_ReturnNull_When_GetByIdAsyncCalledForArchivedOrganisation_Integration()
    {
        var archived = NewOrganisation("Archived Venue", archived: true);
        await SeedAsync(new[] { archived });

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var result = await repo.GetByIdAsync(archived.Id, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task Should_ReturnOnlyArchivedOrganisationsOrderedByName_When_GetArchivedAsyncCalled_Integration()
    {
        await SeedAsync(new[]
        {
            NewOrganisation("Town Hall"),
            NewOrganisation("Zed Hire", archived: true),
            NewOrganisation("Bygone Theatre", archived: true)
        });

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var result = await repo.GetArchivedAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Bygone Theatre", "Zed Hire" }, result.Select(o => o.Name));
    }

    // --- GetByIdIncludingArchivedAsync ---

    [Fact]
    public async Task Should_FindOrganisation_When_GetByIdIncludingArchivedAsyncCalledForArchivedOrganisation_Integration()
    {
        var archived = NewOrganisation("Archived Venue", archived: true);
        await SeedAsync(new[] { archived });

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var result = await repo.GetByIdIncludingArchivedAsync(archived.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.True(result.IsDeleted);
        Assert.Equal("Archived Venue", result.Name);
    }

    [Fact]
    public async Task Should_FindOrganisation_When_GetByIdIncludingArchivedAsyncCalledForActiveOrganisation_Integration()
    {
        var active = NewOrganisation("Town Hall");
        await SeedAsync(new[] { active });

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var result = await repo.GetByIdIncludingArchivedAsync(active.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.False(result.IsDeleted);
    }

    [Fact]
    public async Task Should_ReturnNull_When_GetByIdIncludingArchivedAsyncCalledForUnknownId_Integration()
    {
        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var result = await repo.GetByIdIncludingArchivedAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // --- Contact count excludes removed contacts ---

    [Fact]
    public async Task Should_ExcludeRemovedContacts_When_GetAllAsyncLoadsContacts_Integration()
    {
        var org = NewOrganisation("Town Hall");
        await SeedAsync(
            new[] { org },
            new[]
            {
                NewContact(org.Id, "Venue Manager"),
                NewContact(org.Id, "Box Office"),
                NewContact(org.Id, "Former Manager", removed: true)
            });

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var result = await repo.GetAllAsync(TestContext.Current.CancellationToken);

        var loaded = Assert.Single(result);
        Assert.Equal(2, loaded.Contacts.Count);
        Assert.DoesNotContain(loaded.Contacts, c => c.IsDeleted);
    }

    [Fact]
    public async Task Should_ExcludeRemovedContacts_When_GetArchivedAsyncLoadsContacts_Integration()
    {
        var org = NewOrganisation("Archived Venue", archived: true);
        await SeedAsync(
            new[] { org },
            new[]
            {
                NewContact(org.Id, "Venue Manager"),
                NewContact(org.Id, "Former Manager", removed: true)
            });

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var result = await repo.GetArchivedAsync(TestContext.Current.CancellationToken);

        var loaded = Assert.Single(result);
        var contact = Assert.Single(loaded.Contacts);
        Assert.Equal("Venue Manager", contact.Name);
    }

    [Fact]
    public async Task Should_ExcludeRemovedContacts_When_GetByIdIncludingArchivedAsyncLoadsContacts_Integration()
    {
        var org = NewOrganisation("Archived Venue", archived: true);
        await SeedAsync(
            new[] { org },
            new[]
            {
                NewContact(org.Id, "Venue Manager"),
                NewContact(org.Id, "Former Manager", removed: true)
            });

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var result = await repo.GetByIdIncludingArchivedAsync(org.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        var contact = Assert.Single(result.Contacts);
        Assert.Equal("Venue Manager", contact.Name);
    }

    [Fact]
    public async Task Should_ReturnEmptyContacts_When_AllContactsRemoved_Integration()
    {
        var org = NewOrganisation("Town Hall");
        await SeedAsync(new[] { org }, new[] { NewContact(org.Id, "Former Manager", removed: true) });

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var result = await repo.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Empty(Assert.Single(result).Contacts);
    }

    // --- IsActiveNameInUseAsync ---

    [Fact]
    public async Task Should_ReturnTrue_When_ActiveNameMatchesIgnoringCaseAndWhitespace_Integration()
    {
        await SeedAsync(new[] { NewOrganisation("Town Hall") });

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var inUse = await repo.IsActiveNameInUseAsync("  town HALL  ", ct: TestContext.Current.CancellationToken);

        Assert.True(inUse);
    }

    [Fact]
    public async Task Should_ReturnTrue_When_StoredNameHasSurroundingWhitespace_Integration()
    {
        await SeedAsync(new[] { NewOrganisation(" Town Hall ") });

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var inUse = await repo.IsActiveNameInUseAsync("Town Hall", ct: TestContext.Current.CancellationToken);

        Assert.True(inUse);
    }

    [Fact]
    public async Task Should_ReturnTrue_When_NonAsciiNameDiffersOnlyByCase_Integration()
    {
        await SeedAsync(new[] { NewOrganisation("Église Hall") });

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var inUse = await repo.IsActiveNameInUseAsync("église hall", ct: TestContext.Current.CancellationToken);

        Assert.True(inUse);
    }

    [Fact]
    public async Task Should_ReturnFalse_When_OnlyAnArchivedOrganisationHasTheName_Integration()
    {
        await SeedAsync(new[] { NewOrganisation("Old Venue", archived: true) });

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var inUse = await repo.IsActiveNameInUseAsync("old venue", ct: TestContext.Current.CancellationToken);

        Assert.False(inUse);
    }

    [Fact]
    public async Task Should_ReturnFalse_When_ExcludeIdIsTheOrganisationHoldingTheName_Integration()
    {
        var org = NewOrganisation("Town Hall");
        await SeedAsync(new[] { org });

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var inUse = await repo.IsActiveNameInUseAsync("TOWN HALL", org.Id, TestContext.Current.CancellationToken);

        Assert.False(inUse);
    }

    [Fact]
    public async Task Should_ReturnTrue_When_ExcludeIdIsADifferentOrganisation_Integration()
    {
        var holder = NewOrganisation("Town Hall");
        var other = NewOrganisation("Acme Supplies");
        await SeedAsync(new[] { holder, other });

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var inUse = await repo.IsActiveNameInUseAsync("town hall", other.Id, TestContext.Current.CancellationToken);

        Assert.True(inUse);
    }

    [Fact]
    public async Task Should_ReturnFalse_When_NoOrganisationHasTheName_Integration()
    {
        await SeedAsync(new[] { NewOrganisation("Town Hall") });

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var inUse = await repo.IsActiveNameInUseAsync("Civic Centre", ct: TestContext.Current.CancellationToken);

        Assert.False(inUse);
    }

    public void Dispose() => _factory.Dispose();
}
