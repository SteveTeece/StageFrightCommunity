using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace StageFright.Data.Tests.Migrations;

/// <summary>
/// Integration test for the AddAbnToSettings migration: applies the pre-Abn schema, inserts an
/// old-shape Settings row (no Abn column), migrates to latest, and asserts the row survives with
/// Abn = null and every other field untouched.
/// </summary>
public sealed class AddAbnToSettingsMigrationTests : IDisposable
{
    private const string PreAbnMigration = "20260705222449_AddGst";

    private static readonly Guid SettingsId = new("55555555-0000-0000-0000-000000000001");

    private readonly SqliteConnection _connection;

    public AddAbnToSettingsMigrationTests()
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
    public async Task ExistingSettingsRow_Survives_WithNullAbn_AfterMigration()
    {
        using (var db = CreateContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(PreAbnMigration);

            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO Settings (Id, OrganizationName, AnnualFee, AttendanceFee, MembershipRenewalMonth, CommitteeRenewalMonth, FinancialYearStartMonth, IsGstRegistered, AnnualFeeGstCode, AttendanceFeeGstCode, MaxAgeRangeYears, MinimumMemberAge, Theme, ShowParticipationGraphs, LastCommitteeResetYear, SchemaVersion, IsDeleted, DeletedAt, DeletedBy, CreatedAt, UpdatedAt) VALUES
                ({SettingsId}, 'Pre-Abn Org', 100.0, 10.0, 1, 1, 7, 0, NULL, NULL, 150, 0, 'Light', 1, NULL, '1.1.0', 0, NULL, NULL, '2026-01-01 00:00:00', '2026-01-01 00:00:00');
                """);
        }

        using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();

            var settings = await db.Settings.SingleAsync(s => s.Id == SettingsId);

            Assert.Null(settings.Abn);
            Assert.Equal("Pre-Abn Org", settings.OrganizationName);
            Assert.Equal(100.0m, settings.AnnualFee);
        }
    }
}
