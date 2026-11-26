using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Settings;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// SC-005 (spec 032, T070): back up a database holding an organisation register, restore it into a
/// fresh, migrated database, and every organisation, contact and transaction link comes back unchanged.
/// </summary>
public sealed class OrganisationBackupRoundTripTests : IAsyncLifetime
{
    private static readonly DateTime Created = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Deleted = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    private StageFrightDbContext _source = null!;
    private StageFrightDbContext _target = null!;
    private readonly string _recoveryDir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), $"sf_org_recovery_{Guid.NewGuid()}")).FullName;

    public async ValueTask InitializeAsync()
    {
        _source = await NewDatabaseAsync();
        _target = await NewDatabaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var db in new[] { _source, _target })
        {
            await db.Database.CloseConnectionAsync();
            await db.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_RestoreEveryOrganisationContactAndLinkUnchanged_When_BackedUpThenRestoredIntoEmptyDatabase_Integration()
    {
        var ct = TestContext.Current.CancellationToken;
        var hall = new ExternalOrganisation
        {
            Id = Guid.NewGuid(), Name = "Town Hall", Address = "1 Main St", Email = "hall@example.org",
            CreatedAt = Created, UpdatedAt = Created
        };
        var printer = new ExternalOrganisation
        {
            Id = Guid.NewGuid(), Name = "Old Printer", IsDeleted = true, DeletedAt = Deleted, DeletedBy = "treasurer",
            CreatedAt = Created, UpdatedAt = Deleted
        };
        var bookings = new ExternalOrganisationContact
        {
            Id = Guid.NewGuid(), ExternalOrganisationId = hall.Id, Name = "Pat Lee", Role = "Bookings",
            Phone = "02 1234 5678", Mobile = "0400 000 000", Email = "pat@example.org",
            CreatedAt = Created, UpdatedAt = Created
        };
        var removed = new ExternalOrganisationContact
        {
            Id = Guid.NewGuid(), ExternalOrganisationId = printer.Id, Name = "Sam Roe",
            IsDeleted = true, DeletedAt = Deleted, DeletedBy = "treasurer", CreatedAt = Created, UpdatedAt = Deleted
        };
        var hallHire = new JournalEntry
        {
            Id = Guid.NewGuid(), Type = JournalEntryType.ExpensePayment, Date = Created,
            Description = "Hall hire", CreatedAt = Created, ExternalOrganisationId = hall.Id
        };
        var printing = new JournalEntry
        {
            Id = Guid.NewGuid(), Type = JournalEntryType.ExpensePayment, Date = Created,
            Description = "Programmes", CreatedAt = Created, ExternalOrganisationId = printer.Id
        };
        var unlinked = new JournalEntry
        {
            Id = Guid.NewGuid(), Type = JournalEntryType.GeneralJournal, Date = Created,
            Description = "Adjustment", CreatedAt = Created
        };
        _source.ExternalOrganisations.AddRange(hall, printer);
        _source.ExternalOrganisationContacts.AddRange(bookings, removed);
        _source.JournalEntries.AddRange(hallHire, printing, unlinked);
        await _source.SaveChangesAsync(ct);

        var path = Path.Combine(Path.GetTempPath(), $"sf_org_rt_{Guid.NewGuid()}.sfbak");
        try
        {
            await BuildService(_source).ExportAsync(path, ct);
            await BuildService(_target).ImportAsync(path, ct);
            _target.ChangeTracker.Clear();

            var orgs = await _target.ExternalOrganisations.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
            Assert.Equal(2, orgs.Count);
            AssertOrganisation(hall, orgs.Single(o => o.Id == hall.Id));
            AssertOrganisation(printer, orgs.Single(o => o.Id == printer.Id));

            var contacts = await _target.ExternalOrganisationContacts.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
            Assert.Equal(2, contacts.Count);
            AssertContact(bookings, contacts.Single(c => c.Id == bookings.Id));
            AssertContact(removed, contacts.Single(c => c.Id == removed.Id));

            var entries = await _target.JournalEntries.AsNoTracking().ToListAsync(ct);
            Assert.Equal(hall.Id, entries.Single(j => j.Id == hallHire.Id).ExternalOrganisationId);
            Assert.Equal(printer.Id, entries.Single(j => j.Id == printing.Id).ExternalOrganisationId);
            Assert.Null(entries.Single(j => j.Id == unlinked.Id).ExternalOrganisationId);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            // Only this test's own recovery folder: other backup test classes run in parallel and
            // read their recovery copies back from the shared temp directory.
            Directory.Delete(_recoveryDir, recursive: true);
        }
    }

    private static void AssertOrganisation(ExternalOrganisation expected, ExternalOrganisation actual) =>
        Assert.Equal(
            (expected.Name, expected.Address, expected.Email, expected.IsDeleted, expected.DeletedAt, expected.DeletedBy, expected.CreatedAt, expected.UpdatedAt),
            (actual.Name, actual.Address, actual.Email, actual.IsDeleted, actual.DeletedAt, actual.DeletedBy, actual.CreatedAt, actual.UpdatedAt));

    private static void AssertContact(ExternalOrganisationContact expected, ExternalOrganisationContact actual)
    {
        Assert.Equal(
            (expected.ExternalOrganisationId, expected.Name, expected.Role, expected.Phone, expected.Mobile, expected.Email),
            (actual.ExternalOrganisationId, actual.Name, actual.Role, actual.Phone, actual.Mobile, actual.Email));
        Assert.Equal(
            (expected.IsDeleted, expected.DeletedAt, expected.DeletedBy, expected.CreatedAt, expected.UpdatedAt),
            (actual.IsDeleted, actual.DeletedAt, actual.DeletedBy, actual.CreatedAt, actual.UpdatedAt));
    }

    private BackupService BuildService(StageFrightDbContext db)
    {
        var auditSvc = new AuditTrailService(new AuditTrailRepository(db), NullLogger<AuditTrailService>.Instance);
        return new BackupService(new BackupRepository(db), new UnitOfWork(db), auditSvc,
            NullLogger<BackupService>.Instance, RealLocalizer.Instance,
            new TempRecoveryCopyStore(_recoveryDir), new UnusedDestinationPicker());
    }

    private static async Task<StageFrightDbContext> NewDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var db = new StageFrightDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.MigrateAsync();
        return db;
    }

    private sealed class TempRecoveryCopyStore(string directory) : IRecoveryCopyStore
    {
        public string GetRecoveryDirectory() => directory;
    }

    /// <summary>ExportAsync writes to a path directly, so the Save dialog is never shown here.</summary>
    private sealed class UnusedDestinationPicker : IBackupDestinationPicker
    {
        public Task<BackupDestinationResult> SaveAsync(string suggestedFileName, Stream content, CancellationToken ct = default) =>
            throw new InvalidOperationException("The Save dialog is not used by ExportAsync.");
    }
}
