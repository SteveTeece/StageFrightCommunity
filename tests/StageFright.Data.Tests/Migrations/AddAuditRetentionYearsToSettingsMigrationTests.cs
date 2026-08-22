using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace StageFright.Data.Tests.Migrations;

/// <summary>
/// Integration test for the AddAuditRetentionYearsToSettings migration: applies the pre-migration
/// schema, inserts an old-shape Settings row (no AuditRetentionYears column), migrates to latest,
/// and asserts the row survives with AuditRetentionYears backfilled to the documented default (1).
/// </summary>
public sealed class AddAuditRetentionYearsToSettingsMigrationTests : IDisposable
{
    private const string PreMigration = "20260801030745_AddAgmWorkflow";

    private static readonly Guid SettingsId = new("66666666-0000-0000-0000-000000000001");

    private readonly SqliteConnection _connection;

    public AddAuditRetentionYearsToSettingsMigrationTests()
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
    public async Task ExistingSettingsRow_BackfillsAuditRetentionYearsToOne_AfterMigration()
    {
        using (var db = CreateContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(PreMigration, TestContext.Current.CancellationToken);

            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO Settings (Id, OrganizationName, Abn, AnnualFee, AttendanceFee, MembershipRenewalMonth, CommitteeRenewalMonth, FinancialYearStartMonth, IsGstRegistered, AnnualFeeGstCode, AttendanceFeeGstCode, GeneralCommitteeSeatCountTarget, MaxAgeRangeYears, MinimumMemberAge, Theme, ShowParticipationGraphs, SchemaVersion, IsDeleted, DeletedAt, DeletedBy, CreatedAt, UpdatedAt) VALUES
                ({SettingsId}, 'Pre-Retention Org', NULL, 100.0, 10.0, 1, 1, 7, 0, NULL, NULL, NULL, 150, 0, 'Light', 1, '1.1.0', 0, NULL, NULL, '2026-01-01 00:00:00', '2026-01-01 00:00:00');
                """, cancellationToken: TestContext.Current.CancellationToken);
        }

        using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

            var settings = await db.Settings.SingleAsync(s => s.Id == SettingsId, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(1, settings.AuditRetentionYears);
            Assert.Equal("Pre-Retention Org", settings.OrganizationName);
        }
    }
}
