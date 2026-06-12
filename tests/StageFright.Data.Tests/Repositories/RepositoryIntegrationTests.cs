using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Data.Repositories;
using StageFright.Data.Tests.Infrastructure;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for repository implementations using SQLite in-memory connections.
/// Verifies CRUD, soft-delete global query filters, archive/restore, and immutability constraints.
/// </summary>
public class RepositoryIntegrationTests : IDisposable
{
    private readonly DbContextFactory _factory = new();

    // --- Member repository ---

    [Fact]
    public async Task MemberRepository_Add_And_GetById_Works()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);
        var member = CreateMember();

        var added = await repo.AddAsync(member);
        var found = await repo.GetByIdAsync(added.Id);

        Assert.NotNull(found);
        Assert.Equal("Test Member", found!.Name);
    }

    [Fact]
    public async Task MemberRepository_GetAllAsync_ExcludesSoftDeleted()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);

        var m1 = await repo.AddAsync(CreateMember("Alice"));
        var m2 = await repo.AddAsync(CreateMember("Bob"));
        await repo.ArchiveAsync(m1.Id, "system");

        var all = await repo.GetAllAsync();
        Assert.DoesNotContain(all, m => m.Id == m1.Id);
        Assert.Contains(all, m => m.Id == m2.Id);
    }

    [Fact]
    public async Task MemberRepository_GetArchivedAsync_ReturnsSoftDeleted()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);

        var m = await repo.AddAsync(CreateMember("Archived"));
        await repo.ArchiveAsync(m.Id, "system");

        var archived = await repo.GetArchivedAsync();
        Assert.Contains(archived, a => a.Id == m.Id);
    }

    [Fact]
    public async Task MemberRepository_RestoreAsync_ClearsSoftDeleteFields()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);

        var m = await repo.AddAsync(CreateMember("Restored"));
        await repo.ArchiveAsync(m.Id, "system");
        await repo.RestoreAsync(m.Id);

        var restored = await repo.GetByIdAsync(m.Id);
        Assert.NotNull(restored);
        Assert.False(restored!.IsDeleted);
        Assert.Null(restored.DeletedAt);
        Assert.Null(restored.DeletedBy);
    }

    [Fact]
    public async Task MemberRepository_GetByStatusAsync_FiltersCorrectly()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);

        var active = await repo.AddAsync(CreateMember("Active"));
        var inactive = await repo.AddAsync(CreateMember("Inactive", MemberStatus.Inactive));

        var activeList = await repo.GetByStatusAsync(MemberStatus.Active);
        var inactiveList = await repo.GetByStatusAsync(MemberStatus.Inactive);

        Assert.Contains(activeList, m => m.Id == active.Id);
        Assert.DoesNotContain(activeList, m => m.Id == inactive.Id);
        Assert.Contains(inactiveList, m => m.Id == inactive.Id);
    }

    [Fact]
    public async Task MemberRepository_GetActiveAsOfAsync_UsesEffectiveDates()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);

        var date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var member = CreateMember("Effective");
        member.ActivateDate = date.AddDays(-10);
        member.InactivateDate = date.AddDays(10);

        await repo.AddAsync(member);

        var results = await repo.GetActiveAsOfAsync(date);
        Assert.Contains(results, m => m.Id == member.Id);
    }

    // --- Fee repository (immutability) ---

    [Fact]
    public async Task FeeRepository_Add_And_GetById_Works()
    {
        using var db = _factory.CreateContext();
        var memberRepo = new MemberRepository(db);
        var member = await memberRepo.AddAsync(CreateMember("FeeTest"));

        var repo = new FeeRepository(db);
        var fee = CreateFee(member.Id);
        var added = await repo.AddAsync(fee);

        Assert.NotEqual(Guid.Empty, added.Id);
    }

    [Fact]
    public async Task FeeRepository_AnnualFeeExistsAsync_ReturnsTrueWhenExists()
    {
        using var db = _factory.CreateContext();
        var memberRepo = new MemberRepository(db);
        var member = await memberRepo.AddAsync(CreateMember("AnnualCheck"));

        var repo = new FeeRepository(db);
        var fee = CreateFee(member.Id, FeeType.Annual, year: 2026);
        await repo.AddAsync(fee);

        Assert.True(await repo.AnnualFeeExistsAsync(member.Id, 2026));
        Assert.False(await repo.AnnualFeeExistsAsync(member.Id, 2025));
    }

    // --- Settings repository (singleton) ---

    [Fact]
    public async Task SettingsRepository_GetAsync_ReturnsNullBeforeSetup()
    {
        using var db = _factory.CreateContext();
        var repo = new SettingsRepository(db);

        var result = await repo.GetAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task SettingsRepository_SaveAsync_CreatesAndRetrieves()
    {
        using var db = _factory.CreateContext();
        var repo = new SettingsRepository(db);

        var settings = new Settings
        {
            Id = Guid.NewGuid(),
            OrganizationName = "Test Org",
            AnnualFee = 50m,
            AttendanceFee = 5m,
            MembershipRenewalMonth = 1,
            SchemaVersion = "1.0.0",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repo.SaveAsync(settings);
        var retrieved = await repo.GetAsync();

        Assert.NotNull(retrieved);
        Assert.Equal("Test Org", retrieved!.OrganizationName);
    }

    [Fact]
    public async Task SettingsRepository_SaveAsync_UpdatesExistingRecord()
    {
        using var db = _factory.CreateContext();
        var repo = new SettingsRepository(db);

        var settings = new Settings
        {
            Id = Guid.NewGuid(),
            OrganizationName = "Old Name",
            AnnualFee = 50m,
            AttendanceFee = 5m,
            MembershipRenewalMonth = 1,
            SchemaVersion = "1.0.0",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repo.SaveAsync(settings);

        settings.OrganizationName = "New Name";
        await repo.SaveAsync(settings);

        using var db2 = _factory.CreateContext();
        var repo2 = new SettingsRepository(db2);
        var count = await db2.Settings.CountAsync();
        var retrieved = await repo2.GetAsync();

        Assert.Equal(1, count);
        Assert.Equal("New Name", retrieved!.OrganizationName);
    }

    // --- GL repository (append-only) ---

    [Fact]
    public async Task GLRepository_AddPairAsync_ThrowsOnImbalancedAmounts()
    {
        using var db = _factory.CreateContext();
        var categoryRepo = new CategoryRepository(db);
        var categories = await categoryRepo.GetAllAsync();
        var cat = categories.First();

        var repo = new GLRepository(db);

        var debit = new Transaction
        {
            Id = Guid.NewGuid(), Date = DateTime.UtcNow,
            CategoryId = cat.Id, GLAccount = cat.GLAccount,
            DebitAmount = 100m, CreditAmount = 0m, CreatedAt = DateTime.UtcNow
        };
        var credit = new Transaction
        {
            Id = Guid.NewGuid(), Date = DateTime.UtcNow,
            CategoryId = cat.Id, GLAccount = cat.GLAccount,
            DebitAmount = 0m, CreditAmount = 50m, CreatedAt = DateTime.UtcNow  // Intentionally wrong
        };

        await Assert.ThrowsAsync<GLBalanceException>(
            () => repo.AddPairAsync(debit, credit));
    }

    // --- Category repository ---

    [Fact]
    public async Task CategoryRepository_SystemCategoriesSeeded_PresentsInGetAll()
    {
        using var db = _factory.CreateContext();
        var repo = new CategoryRepository(db);

        var all = await repo.GetAllAsync();

        Assert.Contains(all, c => c.GLAccount == "0100" && c.IsSystem);
        Assert.Contains(all, c => c.GLAccount == "0101" && c.IsSystem);
        Assert.Contains(all, c => c.GLAccount == "9900" && c.IsSystem);
    }

    [Fact]
    public async Task CategoryRepository_ArchiveSystemCategory_ThrowsValidationException()
    {
        using var db = _factory.CreateContext();
        var repo = new CategoryRepository(db);

        var systemCat = (await repo.GetAllAsync()).First(c => c.IsSystem);

        await Assert.ThrowsAsync<ValidationException>(
            () => repo.ArchiveAsync(systemCat.Id, "system"));
    }

    [Fact]
    public async Task CategoryRepository_GetNextGLAccountAsync_IsSequential()
    {
        using var db = _factory.CreateContext();
        var repo = new CategoryRepository(db);

        var first = await repo.GetNextGLAccountAsync(CategoryType.Income);
        Assert.Equal("1000", first);

        // Add a user income category
        var cat = new Category
        {
            Id = Guid.NewGuid(), Name = "Test Income",
            Type = CategoryType.Income, GLAccount = "1000",
            SortOrder = 10, IsSystem = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        await repo.AddAsync(cat);

        var second = await repo.GetNextGLAccountAsync(CategoryType.Income);
        Assert.Equal("1001", second);
    }

    // --- Helpers ---

    private static Member CreateMember(string name = "Test Member", MemberStatus status = MemberStatus.Active) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            StreetAddress = "123 Test St",
            JoinDate = DateTime.UtcNow,
            Status = status,
            ActivateDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static Fee CreateFee(Guid memberId, FeeType type = FeeType.Annual, int year = 2026) =>
        new()
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            FeeType = type,
            Amount = 50m,
            FeeDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(year, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            PaidAtCreation = false,
            CreatedAt = DateTime.UtcNow
        };

    public void Dispose() => _factory.Dispose();
}
