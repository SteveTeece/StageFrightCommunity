using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace StageFright.Data.Tests.Migrations;

/// <summary>
/// Integration tests for the AddExternalOrganisations migration (spec 032): journal entries
/// that existed before the migration carry no organisation link afterwards, and the
/// <c>NOCASE</c> unique index on <c>ExternalOrganisations.Name</c> (filtered to
/// <c>[IsDeleted] = 0</c>) refuses a case-only duplicate of an active organisation but
/// allows the same name when the existing organisation is archived.
/// </summary>
public sealed class AddExternalOrganisationsMigrationTests : IDisposable
{
    private const string PreMigration = "20260830130000_ReclassifyInputTaxAsReceivable";

    // SQLite primary result code for any constraint violation (SQLITE_CONSTRAINT).
    private const int SqliteConstraintErrorCode = 19;

    private static readonly Guid JournalEntryId = new("32032032-0000-0000-0000-000000000001");
    private static readonly Guid ExistingOrganisationId = new("32032032-0000-0000-0000-000000000002");
    private static readonly Guid NewOrganisationId = new("32032032-0000-0000-0000-000000000003");

    private readonly SqliteConnection _connection;

    public AddExternalOrganisationsMigrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private StageFrightDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new StageFrightDbContext(options);
    }

    private static Task InsertOrganisationAsync(StageFrightDbContext db, Guid id, string name, int isDeleted) =>
        db.Database.ExecuteSqlAsync($"""
            INSERT INTO ExternalOrganisations (Id, Name, IsDeleted, CreatedAt, UpdatedAt) VALUES
            ({id}, {name}, {isDeleted}, '2026-11-01 00:00:00', '2026-11-01 00:00:00');
            """, cancellationToken: TestContext.Current.CancellationToken);

    [Fact]
    public async Task Should_LeaveOrganisationLinkNull_When_JournalEntryExistedBeforeMigration_Integration()
    {
        using (var db = CreateContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(PreMigration, TestContext.Current.CancellationToken);

            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO JournalEntries (Id, Type, Date, Description, CreatedAt) VALUES
                ({JournalEntryId}, 'ExpensePayment', '2026-10-01 00:00:00', 'Hall hire', '2026-10-01 00:00:00');
                """, cancellationToken: TestContext.Current.CancellationToken);
        }

        using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

            var entry = await db.JournalEntries.AsNoTracking()
                .SingleAsync(j => j.Id == JournalEntryId, TestContext.Current.CancellationToken);
            Assert.Null(entry.ExternalOrganisationId);
            Assert.Equal("Hall hire", entry.Description);
        }
    }

    [Fact]
    public async Task Should_RefuseName_When_ActiveOrganisationDiffersOnlyByCase_Integration()
    {
        using var db = CreateContext();
        await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
        await InsertOrganisationAsync(db, ExistingOrganisationId, "city council", isDeleted: 0);

        var ex = await Assert.ThrowsAsync<SqliteException>(
            () => InsertOrganisationAsync(db, NewOrganisationId, "City Council", isDeleted: 0));

        Assert.Equal(SqliteConstraintErrorCode, ex.SqliteErrorCode);
        Assert.Equal(1, await db.ExternalOrganisations.IgnoreQueryFilters()
            .CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_AllowName_When_MatchingOrganisationIsArchived_Integration()
    {
        using var db = CreateContext();
        await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
        await InsertOrganisationAsync(db, ExistingOrganisationId, "city council", isDeleted: 1);

        await InsertOrganisationAsync(db, NewOrganisationId, "City Council", isDeleted: 0);

        Assert.Equal(2, await db.ExternalOrganisations.IgnoreQueryFilters()
            .CountAsync(TestContext.Current.CancellationToken));
    }
}
