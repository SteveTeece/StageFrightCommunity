using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Data.Repositories;
using StageFright.Data.Tests.Infrastructure;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for <see cref="ExternalOrganisationContactRepository"/> using SQLite in-memory
/// connections (spec 032, T023). Contacts are listed per organisation through
/// <see cref="ExternalOrganisationRepository"/>'s filtered <c>Contacts</c> include, so these tests
/// check that only the requested organisation's active contacts come back, and that removing a
/// contact is a soft delete.
/// </summary>
public class ExternalOrganisationContactRepositoryTests : IDisposable
{
    private readonly DbContextFactory _factory = new();

    [Fact]
    public async Task Should_ReturnOnlyActiveContactsOfRequestedOrganisation_When_ReadByIdIncludingArchived_Integration()
    {
        var ct = TestContext.Current.CancellationToken;
        var (venue, supplier, venueActive, venueRemoved, supplierActive) = await SeedAsync(ct);

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var org = await repo.GetByIdIncludingArchivedAsync(venue.Id, ct);

        Assert.NotNull(org);
        var contact = Assert.Single(org!.Contacts);
        Assert.Equal(venueActive.Id, contact.Id);
        Assert.DoesNotContain(org.Contacts, c => c.Id == venueRemoved.Id);
        Assert.DoesNotContain(org.Contacts, c => c.Id == supplierActive.Id);
    }

    [Fact]
    public async Task Should_ReturnOnlyActiveContactsOfEachOrganisation_When_ReadViaGetAll_Integration()
    {
        var ct = TestContext.Current.CancellationToken;
        var (venue, supplier, venueActive, venueRemoved, supplierActive) = await SeedAsync(ct);

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var orgs = await repo.GetAllAsync(ct);

        var loadedVenue = Assert.Single(orgs, o => o.Id == venue.Id);
        var venueContact = Assert.Single(loadedVenue.Contacts);
        Assert.Equal(venueActive.Id, venueContact.Id);

        var loadedSupplier = Assert.Single(orgs, o => o.Id == supplier.Id);
        var supplierContact = Assert.Single(loadedSupplier.Contacts);
        Assert.Equal(supplierActive.Id, supplierContact.Id);
    }

    [Fact]
    public async Task Should_ReturnOnlyActiveContactsOfArchivedOrganisation_When_ReadByIdIncludingArchived_Integration()
    {
        var ct = TestContext.Current.CancellationToken;
        var (venue, supplier, venueActive, venueRemoved, supplierActive) = await SeedAsync(ct);

        using (var archiveDb = _factory.CreateContext())
        {
            await new ExternalOrganisationRepository(archiveDb).ArchiveAsync(venue.Id, "coordinator", ct);
        }

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationRepository(db);

        var org = await repo.GetByIdIncludingArchivedAsync(venue.Id, ct);

        Assert.NotNull(org);
        Assert.True(org!.IsDeleted);
        var contact = Assert.Single(org.Contacts);
        Assert.Equal(venueActive.Id, contact.Id);
    }

    [Fact]
    public async Task Should_HideRemovedContact_When_GetAllCalledOnContactRepository_Integration()
    {
        var ct = TestContext.Current.CancellationToken;
        var (venue, supplier, venueActive, venueRemoved, supplierActive) = await SeedAsync(ct);

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationContactRepository(db);

        var contacts = await repo.GetAllAsync(ct);

        Assert.Equal(2, contacts.Count);
        Assert.Contains(contacts, c => c.Id == venueActive.Id);
        Assert.Contains(contacts, c => c.Id == supplierActive.Id);
        Assert.DoesNotContain(contacts, c => c.Id == venueRemoved.Id);
    }

    [Fact]
    public async Task Should_ReturnRemovedContact_When_GetArchivedCalledOnContactRepository_Integration()
    {
        var ct = TestContext.Current.CancellationToken;
        var (venue, supplier, venueActive, venueRemoved, supplierActive) = await SeedAsync(ct);

        using var db = _factory.CreateContext();
        var repo = new ExternalOrganisationContactRepository(db);

        var archived = await repo.GetArchivedAsync(ct);

        var contact = Assert.Single(archived);
        Assert.Equal(venueRemoved.Id, contact.Id);
        Assert.True(contact.IsDeleted);
        Assert.NotNull(contact.DeletedAt);
        Assert.Equal("coordinator", contact.DeletedBy);
    }

    [Fact]
    public async Task Should_KeepRemovedContactRowInTable_When_ContactArchived_Integration()
    {
        var ct = TestContext.Current.CancellationToken;
        var (venue, supplier, venueActive, venueRemoved, supplierActive) = await SeedAsync(ct);

        using var db = _factory.CreateContext();

        var row = await db.ExternalOrganisationContacts
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(c => c.Id == venueRemoved.Id, ct);

        Assert.NotNull(row);
        Assert.True(row!.IsDeleted);
        Assert.Equal(venue.Id, row.ExternalOrganisationId);
        Assert.Equal(3, await db.ExternalOrganisationContacts.IgnoreQueryFilters().CountAsync(ct));
    }

    [Fact]
    public async Task Should_ReturnContactAgain_When_RemovedContactRestored_Integration()
    {
        var ct = TestContext.Current.CancellationToken;
        var (venue, supplier, venueActive, venueRemoved, supplierActive) = await SeedAsync(ct);

        using (var restoreDb = _factory.CreateContext())
        {
            await new ExternalOrganisationContactRepository(restoreDb).RestoreAsync(venueRemoved.Id, ct);
        }

        using var db = _factory.CreateContext();
        var org = await new ExternalOrganisationRepository(db).GetByIdIncludingArchivedAsync(venue.Id, ct);

        Assert.NotNull(org);
        Assert.Equal(2, org!.Contacts.Count);
        Assert.Contains(org.Contacts, c => c.Id == venueRemoved.Id && !c.IsDeleted && c.DeletedAt == null && c.DeletedBy == null);
    }

    /// <summary>
    /// Seeds two organisations: the venue with one active and one removed contact, the supplier
    /// with one active contact. The removal goes through the contact repository's ArchiveAsync.
    /// </summary>
    private async Task<(ExternalOrganisation Venue, ExternalOrganisation Supplier,
        ExternalOrganisationContact VenueActive, ExternalOrganisationContact VenueRemoved,
        ExternalOrganisationContact SupplierActive)> SeedAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var venue = new ExternalOrganisation { Id = Guid.NewGuid(), Name = "Town Hall", CreatedAt = now, UpdatedAt = now };
        var supplier = new ExternalOrganisation { Id = Guid.NewGuid(), Name = "Costume Hire Co", CreatedAt = now, UpdatedAt = now };

        var venueActive = NewContact(venue.Id, "Venue Manager", now);
        var venueRemoved = NewContact(venue.Id, "Former Booking Officer", now);
        var supplierActive = NewContact(supplier.Id, "Hire Desk", now);

        using (var db = _factory.CreateContext())
        {
            db.ExternalOrganisations.AddRange(venue, supplier);
            await db.SaveChangesAsync(ct);

            var contactRepo = new ExternalOrganisationContactRepository(db);
            await contactRepo.AddAsync(venueActive, ct);
            await contactRepo.AddAsync(venueRemoved, ct);
            await contactRepo.AddAsync(supplierActive, ct);
        }

        using (var archiveDb = _factory.CreateContext())
        {
            await new ExternalOrganisationContactRepository(archiveDb).ArchiveAsync(venueRemoved.Id, "coordinator", ct);
        }

        return (venue, supplier, venueActive, venueRemoved, supplierActive);
    }

    private static ExternalOrganisationContact NewContact(Guid organisationId, string name, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        ExternalOrganisationId = organisationId,
        Name = name,
        CreatedAt = now,
        UpdatedAt = now,
    };

    public void Dispose() => _factory.Dispose();
}
