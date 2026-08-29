using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace StageFright.Data.Tests.Migrations;

/// <summary>
/// Integration tests for the AddInternationalAccountingSettings migration's effect on
/// <c>Settings.AuditRetentionYears</c> (spec 028, US8 / FR-023, FR-024, SC-010):
/// a fresh dataset must default the retention to five years, while an existing dataset's
/// already-configured value must survive the migration untouched (the migration alters only
/// the column default — it issues no <c>UpdateData</c>).
/// </summary>
public sealed class AuditRetentionDefaultTests : IDisposable
{
    /// <summary>Last migration before AddInternationalAccountingSettings raised the default 1 → 5.</summary>
    private const string PreMigration = "20260828120000_AddLanguageCodeToSettings";

    private static readonly Guid SettingsId = new("55555555-0000-0000-0000-000000000028");

    private readonly SqliteConnection _connection;

    public AuditRetentionDefaultTests()
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
    public async Task FreshDataset_DefaultsAuditRetentionYearsToFive_AfterMigration()
    {
        using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

            // Insert a Settings row WITHOUT specifying AuditRetentionYears so the column
            // default supplied by the migration is what lands.
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO Settings
                    (Id, OrganizationName, AnnualFee, AttendanceFee, MembershipRenewalMonth, CommitteeRenewalMonth,
                     FinancialYearStartMonth, MaxAgeRangeYears, MinimumMemberAge, Theme, ShowParticipationGraphs,
                     SchemaVersion, IsDeleted, CreatedAt, UpdatedAt)
                VALUES
                    ({SettingsId}, 'Fresh Org', 100.0, 10.0, 1, 1, 7, 150, 0, 'Light', 1,
                     '1.1.0', 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00');
                """, cancellationToken: TestContext.Current.CancellationToken);
        }

        using (var db = CreateContext())
        {
            var settings = await db.Settings.SingleAsync(s => s.Id == SettingsId, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(5, settings.AuditRetentionYears);
        }
    }

    [Fact]
    public async Task ExistingSettingsRow_KeepsItsConfiguredAuditRetentionYears_AfterMigration()
    {
        using (var db = CreateContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(PreMigration, TestContext.Current.CancellationToken);

            // An install that deliberately shortened retention to 2 years before this feature shipped.
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO Settings
                    (Id, OrganizationName, AnnualFee, AttendanceFee, MembershipRenewalMonth, CommitteeRenewalMonth,
                     FinancialYearStartMonth, MaxAgeRangeYears, MinimumMemberAge, Theme, ShowParticipationGraphs,
                     SchemaVersion, IsDeleted, CreatedAt, UpdatedAt, AuditRetentionYears)
                VALUES
                    ({SettingsId}, 'Pre-028 Org', 100.0, 10.0, 1, 1, 7, 150, 0, 'Light', 1,
                     '1.1.0', 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00', 2);
                """, cancellationToken: TestContext.Current.CancellationToken);
        }

        using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

            var settings = await db.Settings.SingleAsync(s => s.Id == SettingsId, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(2, settings.AuditRetentionYears);
            Assert.Equal("Pre-028 Org", settings.OrganizationName);
        }
    }
}
