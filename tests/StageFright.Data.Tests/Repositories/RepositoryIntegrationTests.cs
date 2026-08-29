using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Members;
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

        var added = await repo.AddAsync(member, TestContext.Current.CancellationToken);
        var found = await repo.GetByIdAsync(added.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.Equal("Test Member", found!.FullName);
    }

    [Fact]
    public async Task MemberRepository_GetAllAsync_ExcludesSoftDeleted()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);

        var m1 = await repo.AddAsync(CreateMember("Alice"), TestContext.Current.CancellationToken);
        var m2 = await repo.AddAsync(CreateMember("Bob"), TestContext.Current.CancellationToken);
        await repo.ArchiveAsync(m1.Id, "system", TestContext.Current.CancellationToken);

        var all = await repo.GetAllAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(all, m => m.Id == m1.Id);
        Assert.Contains(all, m => m.Id == m2.Id);
    }

    [Fact]
    public async Task MemberRepository_GetArchivedAsync_ReturnsSoftDeleted()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);

        var m = await repo.AddAsync(CreateMember("Archived"), TestContext.Current.CancellationToken);
        await repo.ArchiveAsync(m.Id, "system", TestContext.Current.CancellationToken);

        var archived = await repo.GetArchivedAsync(TestContext.Current.CancellationToken);
        Assert.Contains(archived, a => a.Id == m.Id);
    }

    [Fact]
    public async Task MemberRepository_RestoreAsync_ClearsSoftDeleteFields()
    {
        using var db = _factory.CreateContext();
        var repo = new MemberRepository(db);

        var m = await repo.AddAsync(CreateMember("Restored"), TestContext.Current.CancellationToken);
        await repo.ArchiveAsync(m.Id, "system", TestContext.Current.CancellationToken);
        await repo.RestoreAsync(m.Id, TestContext.Current.CancellationToken);

        var restored = await repo.GetByIdAsync(m.Id, TestContext.Current.CancellationToken);
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

        var active = await repo.AddAsync(CreateMember("Active"), TestContext.Current.CancellationToken);
        var inactive = await repo.AddAsync(CreateMember("Inactive", MemberStatus.Inactive), TestContext.Current.CancellationToken);

        var activeList = await repo.GetByStatusAsync(MemberStatus.Active, TestContext.Current.CancellationToken);
        var inactiveList = await repo.GetByStatusAsync(MemberStatus.Inactive, TestContext.Current.CancellationToken);

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

        await repo.AddAsync(member, TestContext.Current.CancellationToken);

        var results = await repo.GetActiveAsOfAsync(date, TestContext.Current.CancellationToken);
        Assert.Contains(results, m => m.Id == member.Id);
    }

    // --- Fee repository (immutability) ---

    [Fact]
    public async Task FeeRepository_Add_And_GetById_Works()
    {
        using var db = _factory.CreateContext();
        var memberRepo = new MemberRepository(db);
        var member = await memberRepo.AddAsync(CreateMember("FeeTest"), TestContext.Current.CancellationToken);

        var repo = new FeeRepository(db);
        var fee = CreateFee(member.Id);
        var added = await repo.AddAsync(fee, TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, added.Id);
    }

    [Fact]
    public async Task FeeRepository_AnnualFeeExistsAsync_ReturnsTrueWhenExists()
    {
        using var db = _factory.CreateContext();
        var memberRepo = new MemberRepository(db);
        var member = await memberRepo.AddAsync(CreateMember("AnnualCheck"), TestContext.Current.CancellationToken);

        var repo = new FeeRepository(db);
        var fee = CreateFee(member.Id, FeeType.Annual, year: 2026);
        await repo.AddAsync(fee, TestContext.Current.CancellationToken);

        Assert.True(await repo.AnnualFeeExistsAsync(member.Id, 2026, TestContext.Current.CancellationToken));
        Assert.False(await repo.AnnualFeeExistsAsync(member.Id, 2025, TestContext.Current.CancellationToken));
    }

    // --- Settings repository (singleton) ---

    [Fact]
    public async Task SettingsRepository_GetAsync_ReturnsNullBeforeSetup()
    {
        using var db = _factory.CreateContext();
        var repo = new SettingsRepository(db);

        var result = await repo.GetAsync(TestContext.Current.CancellationToken);
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

        await repo.SaveAsync(settings, TestContext.Current.CancellationToken);
        var retrieved = await repo.GetAsync(TestContext.Current.CancellationToken);

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

        await repo.SaveAsync(settings, TestContext.Current.CancellationToken);

        settings.OrganizationName = "New Name";
        await repo.SaveAsync(settings, TestContext.Current.CancellationToken);

        using var db2 = _factory.CreateContext();
        var repo2 = new SettingsRepository(db2);
        var count = await db2.Settings.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        var retrieved = await repo2.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
        Assert.Equal("New Name", retrieved!.OrganizationName);
    }

    // --- GL repository (append-only) ---

    [Fact]
    public async Task GLRepository_AddPairAsync_ThrowsOnImbalancedAmounts()
    {
        using var db = _factory.CreateContext();
        var accountRepo = new AccountRepository(db);
        var accounts = await accountRepo.GetAllAsync(TestContext.Current.CancellationToken);
        var cat = accounts.First();

        var repo = new GLRepository(db, new ClosedPeriodGuard(new SettingsRepository(db)));

        var debit = new Transaction
        {
            Id = Guid.NewGuid(), Date = DateTime.UtcNow,
            AccountId = cat.Id, GLAccount = cat.AccountNumber,
            DebitAmount = 100m, CreditAmount = 0m, CreatedAt = DateTime.UtcNow
        };
        var credit = new Transaction
        {
            Id = Guid.NewGuid(), Date = DateTime.UtcNow,
            AccountId = cat.Id, GLAccount = cat.AccountNumber,
            DebitAmount = 0m, CreditAmount = 50m, CreatedAt = DateTime.UtcNow  // Intentionally wrong
        };

        await Assert.ThrowsAsync<GLBalanceException>(
            () => repo.AddPairAsync(debit, credit, TestContext.Current.CancellationToken));
    }

    // --- Account repository ---

    [Fact]
    public async Task AccountRepository_SystemAccountsSeeded_PresentsInGetAll()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        var all = await repo.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Contains(all, c => c.AccountNumber == "1100" && c.IsSystem);
        Assert.Contains(all, c => c.AccountNumber == "1200" && c.IsSystem);
        Assert.Contains(all, c => c.AccountNumber == "6999" && c.IsSystem);
    }

    [Fact]
    public async Task AccountRepository_ArchiveSystemAccount_ThrowsValidationException()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        var systemCat = (await repo.GetAllAsync(TestContext.Current.CancellationToken)).First(c => c.IsSystem);

        await Assert.ThrowsAsync<ValidationException>(
            () => repo.ArchiveAsync(systemCat.Id, "system", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AccountRepository_GetNextAccountNumberAsync_IsSequential()
    {
        using var db = _factory.CreateContext();
        var repo = new AccountRepository(db);

        var first = await repo.GetNextAccountNumberAsync(AccountType.Income, false, TestContext.Current.CancellationToken);
        Assert.Equal("4000", first);

        // Add a user income account
        var cat = new Account
        {
            Id = Guid.NewGuid(), Name = "Test Income",
            Type = AccountType.Income, AccountNumber = "4000",
            SortOrder = 10, IsSystem = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        await repo.AddAsync(cat, TestContext.Current.CancellationToken);

        var second = await repo.GetNextAccountNumberAsync(AccountType.Income, false, TestContext.Current.CancellationToken);
        Assert.Equal("4001", second);
    }

    // --- Attendance repository ---

    [Fact]
    public async Task AttendanceRepository_GetByRehearsalAsync_OrdersByMemberLastNameThenFirstName()
    {
        using var db = _factory.CreateContext();
        var memberRepo = new MemberRepository(db);
        var rehearsalRepo = new RehearsalRepository(db);
        var attendanceRepo = new AttendanceRepository(db);

        var zoe = await memberRepo.AddAsync(CreateMemberWithNames("Zoe", "Adams"), TestContext.Current.CancellationToken);
        var alice = await memberRepo.AddAsync(CreateMemberWithNames("Alice", "Baker"), TestContext.Current.CancellationToken);
        var bob = await memberRepo.AddAsync(CreateMemberWithNames("Bob", "Adams"), TestContext.Current.CancellationToken);

        var rehearsal = await rehearsalRepo.AddAsync(new Rehearsal
        {
            Id = Guid.NewGuid(), Date = DateTime.UtcNow.Date, Time = new TimeSpan(19, 0, 0),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        }, TestContext.Current.CancellationToken);

        await attendanceRepo.AddBatchAsync([
            new AttendanceRecord { Id = Guid.NewGuid(), RehearsalId = rehearsal.Id, MemberId = zoe.Id, Attended = true, CreatedAt = DateTime.UtcNow },
            new AttendanceRecord { Id = Guid.NewGuid(), RehearsalId = rehearsal.Id, MemberId = alice.Id, Attended = true, CreatedAt = DateTime.UtcNow },
            new AttendanceRecord { Id = Guid.NewGuid(), RehearsalId = rehearsal.Id, MemberId = bob.Id, Attended = false, CreatedAt = DateTime.UtcNow }
        ], TestContext.Current.CancellationToken);

        // Real EF-translated SQL query — must sort by the mapped LastName/FirstName columns,
        // not the unmapped computed SortableFullName property (which would throw
        // InvalidOperationException at runtime instead of translating to SQL; see T042).
        var result = await attendanceRepo.GetByRehearsalAsync(rehearsal.Id, TestContext.Current.CancellationToken);

        Assert.Equal([bob.Id, zoe.Id, alice.Id], result.Select(r => r.MemberId));
    }

    // --- Helpers ---

    private static Member CreateMemberWithNames(string firstName, string lastName) => new()
    {
        Id = Guid.NewGuid(), FirstName = firstName, LastName = lastName,
        StreetAddress = "123 Test St", JoinDate = DateTime.UtcNow,
        Status = MemberStatus.Active, ActivateDate = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Member CreateMember(string name = "Test Member", MemberStatus status = MemberStatus.Active)
    {
        var (firstName, lastName) = MemberNameSplitter.Split(name);
        return new()
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            StreetAddress = "123 Test St",
            JoinDate = DateTime.UtcNow,
            Status = status,
            ActivateDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

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
