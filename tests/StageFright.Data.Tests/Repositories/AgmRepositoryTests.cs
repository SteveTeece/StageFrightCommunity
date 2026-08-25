using StageFright.Core.Entities;
using StageFright.Data.Repositories;
using StageFright.Data.Tests.Infrastructure;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for AgmRepository.ExistsForYearAsync using SQLite in-memory connections —
/// the one-non-archived-AGM-per-calendar-year enforcement mechanism (FR-015).
/// </summary>
public class AgmRepositoryTests : IDisposable
{
    private readonly DbContextFactory _factory = new();

    [Fact]
    public async Task ExistsForYearAsync_ReturnsTrue_WhenNonArchivedAgmFallsInYear()
    {
        using var db = _factory.CreateContext();
        db.AnnualGeneralMeetings.Add(new AnnualGeneralMeeting
        {
            Id = Guid.NewGuid(), Date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var freshDb = _factory.CreateContext();
        var repo = new AgmRepository(freshDb);

        var exists = await repo.ExistsForYearAsync(2026, TestContext.Current.CancellationToken);

        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsForYearAsync_ReturnsFalse_WhenNoAgmExistsForYear()
    {
        using var db = _factory.CreateContext();
        var repo = new AgmRepository(db);

        var exists = await repo.ExistsForYearAsync(2026, TestContext.Current.CancellationToken);

        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsForYearAsync_ReturnsFalse_ForArchivedAgmInThatYear()
    {
        using var db = _factory.CreateContext();
        db.AnnualGeneralMeetings.Add(new AnnualGeneralMeeting
        {
            Id = Guid.NewGuid(), Date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            IsDeleted = true, DeletedAt = DateTime.UtcNow, DeletedBy = "coordinator",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var freshDb = _factory.CreateContext();
        var repo = new AgmRepository(freshDb);

        var exists = await repo.ExistsForYearAsync(2026, TestContext.Current.CancellationToken);

        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsForYearAsync_ReturnsFalse_ForDifferentCalendarYear()
    {
        using var db = _factory.CreateContext();
        db.AnnualGeneralMeetings.Add(new AnnualGeneralMeeting
        {
            Id = Guid.NewGuid(), Date = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var freshDb = _factory.CreateContext();
        var repo = new AgmRepository(freshDb);

        var exists = await repo.ExistsForYearAsync(2026, TestContext.Current.CancellationToken);

        Assert.False(exists);
    }

    public void Dispose() { }
}
