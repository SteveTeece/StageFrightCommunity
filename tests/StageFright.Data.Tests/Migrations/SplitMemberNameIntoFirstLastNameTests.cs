using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace StageFright.Data.Tests.Migrations;

/// <summary>
/// Integration tests for the SplitMemberNameIntoFirstLastName migration: applies the
/// pre-split schema, inserts legacy single-Name rows (two-word, mononym, irregular
/// whitespace, overlong, archived), migrates to latest, and asserts the SQL-based
/// split matches the same expected outputs as MemberNameSplitterTests, with zero
/// records lost and Status/IsDeleted unchanged.
/// </summary>
public sealed class SplitMemberNameIntoFirstLastNameTests : IDisposable
{
    private const string PreSplitMigration = "20260708050050_AddAbnToSettings";

    private static readonly Guid TwoWordId = new("11111111-0000-0000-0000-000000000001");
    private static readonly Guid MononymId = new("11111111-0000-0000-0000-000000000002");
    private static readonly Guid WhitespaceId = new("11111111-0000-0000-0000-000000000003");
    private static readonly Guid OverlongId = new("11111111-0000-0000-0000-000000000004");
    private static readonly Guid ArchivedId = new("11111111-0000-0000-0000-000000000005");

    private readonly SqliteConnection _connection;

    public SplitMemberNameIntoFirstLastNameTests()
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

    private async Task SeedPreSplitDatabaseAsync()
    {
        using var db = CreateContext();
        await db.GetService<IMigrator>().MigrateAsync(PreSplitMigration);

        var overlongName = new string('A', 150) + " " + new string('B', 150);

        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO Members (Id, Name, StreetAddress, Phone, Email, JoinDate, DateOfBirth, Status, ActivateDate, InactivateDate, IsDeleted, DeletedAt, DeletedBy, CreatedAt, UpdatedAt) VALUES
            ({TwoWordId}, 'Jane Smith', '1 Test St', NULL, NULL, '2025-01-01 00:00:00', NULL, 'Active', '2025-01-01 00:00:00', NULL, 0, NULL, NULL, '2025-01-01 00:00:00', '2025-01-01 00:00:00'),
            ({MononymId}, 'Cher', '2 Test St', NULL, NULL, '2025-01-01 00:00:00', NULL, 'Active', '2025-01-01 00:00:00', NULL, 0, NULL, NULL, '2025-01-01 00:00:00', '2025-01-01 00:00:00'),
            ({WhitespaceId}, '  Jane    Smith  ', '3 Test St', NULL, NULL, '2025-01-01 00:00:00', NULL, 'Active', '2025-01-01 00:00:00', NULL, 0, NULL, NULL, '2025-01-01 00:00:00', '2025-01-01 00:00:00'),
            ({OverlongId}, {overlongName}, '4 Test St', NULL, NULL, '2025-01-01 00:00:00', NULL, 'Active', '2025-01-01 00:00:00', NULL, 0, NULL, NULL, '2025-01-01 00:00:00', '2025-01-01 00:00:00'),
            ({ArchivedId}, 'Old Member', '5 Test St', NULL, NULL, '2024-01-01 00:00:00', NULL, 'Inactive', NULL, '2025-06-01 00:00:00', 1, '2025-06-01 00:00:00', 'system', '2024-01-01 00:00:00', '2024-01-01 00:00:00');
            """);

        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Settings (Id, OrganizationName, AnnualFee, AttendanceFee, MembershipRenewalMonth, CommitteeRenewalMonth, MaxAgeRangeYears, MinimumMemberAge, Theme, ShowParticipationGraphs, LastCommitteeResetYear, SchemaVersion, IsDeleted, DeletedAt, DeletedBy, CreatedAt, UpdatedAt) VALUES
            ('44444444-0000-0000-0000-000000000001', 'Legacy Org', 100.0, 10.0, 1, 1, 150, 0, 'Light', 1, NULL, '1.0.0', 0, NULL, NULL, '2025-01-01 00:00:00', '2025-01-01 00:00:00');
            """);
    }

    private async Task MigrateToLatestAsync()
    {
        using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    [Fact]
    public async Task Should_PreserveRowCount_When_Migrated()
    {
        await SeedPreSplitDatabaseAsync();
        await MigrateToLatestAsync();

        using var db = CreateContext();
        var count = await db.Members.IgnoreQueryFilters().CountAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(5, count);
    }

    [Fact]
    public async Task Should_SplitTwoWordName_When_Migrated()
    {
        await SeedPreSplitDatabaseAsync();
        await MigrateToLatestAsync();

        using var db = CreateContext();
        var member = await db.Members.SingleAsync(m => m.Id == TwoWordId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Jane", member.FirstName);
        Assert.Equal("Smith", member.LastName);
    }

    [Fact]
    public async Task Should_LeaveLastNameBlank_ForMononym_When_Migrated()
    {
        await SeedPreSplitDatabaseAsync();
        await MigrateToLatestAsync();

        using var db = CreateContext();
        var member = await db.Members.SingleAsync(m => m.Id == MononymId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Cher", member.FirstName);
        Assert.Equal(string.Empty, member.LastName);
    }

    [Fact]
    public async Task Should_CollapseIrregularWhitespace_BeforeSplitting_When_Migrated()
    {
        await SeedPreSplitDatabaseAsync();
        await MigrateToLatestAsync();

        using var db = CreateContext();
        var member = await db.Members.SingleAsync(m => m.Id == WhitespaceId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Jane", member.FirstName);
        Assert.Equal("Smith", member.LastName);
    }

    [Fact]
    public async Task Should_TruncateOverlongSides_ToOneHundredCharacters_When_Migrated()
    {
        await SeedPreSplitDatabaseAsync();
        await MigrateToLatestAsync();

        using var db = CreateContext();
        var member = await db.Members.SingleAsync(m => m.Id == OverlongId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(100, member.FirstName.Length);
        Assert.Equal(100, member.LastName.Length);
        Assert.Equal(new string('A', 100), member.FirstName);
        Assert.Equal(new string('B', 100), member.LastName);
    }

    [Fact]
    public async Task Should_ConvertArchivedMember_Identically_WithStatusAndSoftDeletePreserved_When_Migrated()
    {
        await SeedPreSplitDatabaseAsync();
        await MigrateToLatestAsync();

        using var db = CreateContext();
        var member = await db.Members.IgnoreQueryFilters().SingleAsync(m => m.Id == ArchivedId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Old", member.FirstName);
        Assert.Equal("Member", member.LastName);
        Assert.Equal(Core.Enums.MemberStatus.Inactive, member.Status);
        Assert.True(member.IsDeleted);
    }
}
