using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace StageFright.Data.Tests.Migrations;

/// <summary>
/// Integration test for the AddLanguageCodeToSettings migration (spec 027, US3 / T051): applies
/// the pre-migration schema, inserts an old-shape Settings row (no LanguageCode column),
/// migrates to latest, and asserts the row survives with LanguageCode null (existing installs
/// have no explicit language preference — FR-017). A second test round-trips a set value.
/// </summary>
public sealed class AddLanguageCodeToSettingsMigrationTests : IDisposable
{
    private const string PreMigration = "20260823041629_AddIsRecordedToAgm";

    private static readonly Guid SettingsId = new("77777777-0000-0000-0000-000000000001");

    private readonly SqliteConnection _connection;

    public AddLanguageCodeToSettingsMigrationTests()
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
    public async Task ExistingSettingsRow_HasNullLanguageCode_AfterMigration()
    {
        using (var db = CreateContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(PreMigration, TestContext.Current.CancellationToken);

            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO Settings
                    (Id, OrganizationName, AnnualFee, AttendanceFee, MembershipRenewalMonth, CommitteeRenewalMonth,
                     FinancialYearStartMonth, MaxAgeRangeYears, MinimumMemberAge, Theme, ShowParticipationGraphs,
                     SchemaVersion, IsDeleted, CreatedAt, UpdatedAt)
                VALUES
                    ({SettingsId}, 'Pre-Language Org', 100.0, 10.0, 1, 1, 7, 150, 0, 'Light', 1,
                     '1.1.0', 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00');
                """, cancellationToken: TestContext.Current.CancellationToken);
        }

        using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

            var settings = await db.Settings.SingleAsync(s => s.Id == SettingsId, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Null(settings.LanguageCode);
            Assert.Equal("Pre-Language Org", settings.OrganizationName);
        }
    }

    [Fact]
    public async Task LanguageCode_RoundTrips_WhenSetAndReloaded()
    {
        using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

            db.Settings.Add(new Core.Entities.Settings
            {
                Id = SettingsId,
                OrganizationName = "Language Org",
                AnnualFee = 100m,
                AttendanceFee = 10m,
                MembershipRenewalMonth = 1,
                CommitteeRenewalMonth = 1,
                FinancialYearStartMonth = 7,
                MaxAgeRangeYears = 150,
                MinimumMemberAge = 0,
                Theme = Core.Enums.Theme.Dark,
                LanguageCode = "en-AU",
                SchemaVersion = "1.1.0",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var db = CreateContext())
        {
            var settings = await db.Settings.SingleAsync(s => s.Id == SettingsId, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("en-AU", settings.LanguageCode);
        }
    }
}
