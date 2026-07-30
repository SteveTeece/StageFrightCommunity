using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Data.Repositories;
using StageFright.Data.Tests.Infrastructure;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for MemberRepository — GetByStatus, GetActiveAsOfAsync, and soft-delete filtering.
/// </summary>
public class MemberRepositoryIntegrationTests : IDisposable
{
    private readonly DbContextFactory _factory = new();

    // --- GetByStatusAsync ---

    [Fact]
    public async Task GetByStatusAsync_ReturnsOnlyActiveMembers_WhenFilteredActive()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);

        var active = await repo.AddAsync(Member("Alice", MemberStatus.Active));
        var inactive = await repo.AddAsync(Member("Bob", MemberStatus.Inactive));

        var result = await repo.GetByStatusAsync(MemberStatus.Active);

        Assert.Contains(result, m => m.Id == active.Id);
        Assert.DoesNotContain(result, m => m.Id == inactive.Id);
    }

    [Fact]
    public async Task GetByStatusAsync_ReturnsOnlyInactiveMembers_WhenFilteredInactive()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);

        await repo.AddAsync(Member("Active One", MemberStatus.Active));
        var inactive = await repo.AddAsync(Member("Inactive One", MemberStatus.Inactive));

        var result = await repo.GetByStatusAsync(MemberStatus.Inactive);

        Assert.Contains(result, m => m.Id == inactive.Id);
        Assert.All(result, m => Assert.Equal(MemberStatus.Inactive, m.Status));
    }

    [Fact]
    public async Task GetByStatusAsync_ExcludesArchived_EvenIfStatusMatchesFilter()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);

        var member = await repo.AddAsync(Member("Archived Active", MemberStatus.Active));
        await repo.ArchiveAsync(member.Id, "system");

        var result = await repo.GetByStatusAsync(MemberStatus.Active);

        Assert.DoesNotContain(result, m => m.Id == member.Id);
    }

    // --- GetActiveAsOfAsync ---

    [Fact]
    public async Task GetActiveAsOfAsync_ReturnsMember_WhenActiveOnDate()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);

        var refDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var m = Member("Effective Alice", MemberStatus.Active);
        m.ActivateDate = refDate.AddDays(-5);
        m.InactivateDate = null;
        await repo.AddAsync(m);

        var result = await repo.GetActiveAsOfAsync(refDate);

        Assert.Contains(result, r => r.Id == m.Id);
    }

    [Fact]
    public async Task GetActiveAsOfAsync_ExcludesMember_WhenActivatedAfterDate()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);

        var refDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var m = Member("Late Joiner", MemberStatus.Active);
        m.ActivateDate = refDate.AddDays(5); // activated AFTER reference date
        await repo.AddAsync(m);

        var result = await repo.GetActiveAsOfAsync(refDate);

        Assert.DoesNotContain(result, r => r.Id == m.Id);
    }

    [Fact]
    public async Task GetActiveAsOfAsync_ExcludesMember_WhenInactivatedBeforeDate()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);

        var refDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var m = Member("Early Leaver", MemberStatus.Inactive);
        m.ActivateDate = refDate.AddDays(-20);
        m.InactivateDate = refDate.AddDays(-10); // inactivated BEFORE reference date
        await repo.AddAsync(m);

        var result = await repo.GetActiveAsOfAsync(refDate);

        Assert.DoesNotContain(result, r => r.Id == m.Id);
    }

    [Fact]
    public async Task GetActiveAsOfAsync_ReturnsMember_WhenInactivatedAfterDate()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);

        var refDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var m = Member("Later Leaver", MemberStatus.Inactive);
        m.ActivateDate = refDate.AddDays(-20);
        m.InactivateDate = refDate.AddDays(10); // inactivated AFTER reference date -> was still active on refDate
        await repo.AddAsync(m);

        var result = await repo.GetActiveAsOfAsync(refDate);

        Assert.Contains(result, r => r.Id == m.Id);
    }

    [Fact]
    public async Task GetActiveAsOfAsync_ExcludesArchivedMembers()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);

        var refDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var m = Member("Archived", MemberStatus.Active);
        m.ActivateDate = refDate.AddDays(-5);
        await repo.AddAsync(m);
        await repo.ArchiveAsync(m.Id, "system");

        var result = await repo.GetActiveAsOfAsync(refDate);

        Assert.DoesNotContain(result, r => r.Id == m.Id);
    }

    // --- GetAllAsync soft-delete filter ---

    [Fact]
    public async Task GetAllAsync_ExcludesArchivedMembers()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);

        var normal = await repo.AddAsync(Member("Normal"));
        var archived = await repo.AddAsync(Member("Archived"));
        await repo.ArchiveAsync(archived.Id, "system");

        var result = await repo.GetAllAsync();

        Assert.Contains(result, m => m.Id == normal.Id);
        Assert.DoesNotContain(result, m => m.Id == archived.Id);
    }

    // --- Helpers ---

    private static Member Member(string name, MemberStatus status = MemberStatus.Active)
    {
        var (firstName, lastName) = StageFright.Core.Modules.Members.MemberNameSplitter.Split(name);
        return new()
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            StreetAddress = "1 Test St",
            Status = status,
            ActivateDate = DateTime.UtcNow.AddDays(-1),
            JoinDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Dispose() => _factory.Dispose();
}
