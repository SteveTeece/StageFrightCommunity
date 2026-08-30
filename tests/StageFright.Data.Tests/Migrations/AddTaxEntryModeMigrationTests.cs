using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StageFright.Core.Enums;

namespace StageFright.Data.Tests.Migrations;

/// <summary>
/// Integration test for the AddTaxEntryMode migration (spec 028, issue #354): a Settings row
/// created before the migration is backfilled to <see cref="TaxEntryMode.Inclusive"/> by the
/// column's <c>NOT NULL DEFAULT 'Inclusive'</c>, so every pre-#354 dataset keeps today's
/// tax-inclusive entry behaviour.
/// </summary>
public sealed class AddTaxEntryModeMigrationTests : IDisposable
{
    private const string PreMigration = "20260830013457_AddOrganisationInceptionDate";

    private static readonly Guid SettingsId = new("88888888-0000-0000-0000-000000000001");

    private readonly SqliteConnection _connection;

    public AddTaxEntryModeMigrationTests()
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

    [Fact]
    public async Task ExistingSettingsRow_BackfillsToInclusive_AfterMigration()
    {
        using (var db = CreateContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(PreMigration, TestContext.Current.CancellationToken);

            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO Settings (Id, OrganizationName, AnnualFee, AttendanceFee, MembershipRenewalMonth, CommitteeRenewalMonth, FinancialYearStartMonth, FinancialYearStartDay, CurrencyCode, IsTaxApplicable, MaxAgeRangeYears, MinimumMemberAge, Theme, ShowParticipationGraphs, AuditRetentionYears, SchemaVersion, IsDeleted, CreatedAt, UpdatedAt) VALUES
                ({SettingsId}, 'Legacy Org', 100.0, 10.0, 1, 1, 7, 1, 'AUD', 0, 150, 0, 'Dark', 1, 5, '1.1.0', 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00');
                """, cancellationToken: TestContext.Current.CancellationToken);
        }

        using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

            var settings = await db.Settings.SingleAsync(s => s.Id == SettingsId, TestContext.Current.CancellationToken);
            Assert.Equal(TaxEntryMode.Inclusive, settings.TaxEntryMode);
        }
    }
}
