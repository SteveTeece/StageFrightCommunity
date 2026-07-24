using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Members;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V2 member management: create, DOB/age, validation, status transitions, committee history.
/// Uses a real SQLite in-memory database with full EF migrations applied.
/// </summary>
public sealed class V2_MemberManagementTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- Create ---

    [Fact]
    public async Task CreateMember_WithoutDob_StoresActiveStatus()
    {
        var svc = BuildMemberService();

        var member = await svc.CreateAsync(new CreateMemberRequest
        {
            Name = "John Smith",
            StreetAddress = "1 Main St",
            JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        Assert.Equal(MemberStatus.Active, member.Status);
        Assert.NotNull(member.ActivateDate);
        Assert.Null(member.DateOfBirth);
    }

    [Fact]
    public async Task CreateMember_WithDob_StoresDateOfBirth()
    {
        var svc = BuildMemberService();
        var dob = new DateTime(1990, 3, 15, 0, 0, 0, DateTimeKind.Utc);

        var member = await svc.CreateAsync(new CreateMemberRequest
        {
            Name = "Alice Blue",
            StreetAddress = "2 Park Ave",
            JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DateOfBirth = dob
        });

        Assert.Equal(dob, member.DateOfBirth);
    }

    [Fact]
    public async Task CreateMember_AgeIsCalculated_FromDob()
    {
        var svc = BuildMemberService();
        var ageCalc = new AgeCalculationService();

        var dob = new DateTime(1990, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var member = await svc.CreateAsync(new CreateMemberRequest
        {
            Name = "Bob Green",
            StreetAddress = "3 Elm St",
            JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DateOfBirth = dob
        });

        var age = ageCalc.Calculate(member.DateOfBirth, DateTime.UtcNow.Date);
        Assert.NotNull(age);
        Assert.True(age > 0);
    }

    // --- Validation ---

    [Fact]
    public async Task CreateMember_InvalidEmail_Throws_ValidationException()
    {
        var svc = BuildMemberService();

        await Assert.ThrowsAsync<ValidationException>(() =>
            svc.CreateAsync(new CreateMemberRequest
            {
                Name = "Test User",
                StreetAddress = "1 Test St",
                Email = "not-an-email",
                JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }));
    }

    [Fact]
    public async Task CreateMember_FutureDob_Throws_ValidationException_WithPastMessage()
    {
        var svc = BuildMemberService();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            svc.CreateAsync(new CreateMemberRequest
            {
                Name = "Future Person",
                StreetAddress = "1 Future Ln",
                JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                DateOfBirth = DateTime.UtcNow.Date.AddDays(1)
            }));

        Assert.Contains("past", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateMember_EmptyName_Throws_ValidationException()
    {
        var svc = BuildMemberService();

        await Assert.ThrowsAsync<ValidationException>(() =>
            svc.CreateAsync(new CreateMemberRequest
            {
                Name = "",
                StreetAddress = "1 Test St",
                JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }));
    }

    [Fact]
    public async Task CreateMember_DobExceedsMaxAgeRange_ThrowsValidationException()
    {
        await SeedSettingsAsync(maxAgeRangeYears: 100, minimumMemberAge: 0);
        var svc = BuildMemberService();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            svc.CreateAsync(new CreateMemberRequest
            {
                Name = "Very Old Person",
                StreetAddress = "1 Ancient Ln",
                JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                DateOfBirth = DateTime.UtcNow.Date.AddYears(-101)
            }));

        Assert.Contains("100", ex.Message);
    }

    [Fact]
    public async Task CreateMember_DobBelowMinimumAge_ThrowsValidationException()
    {
        await SeedSettingsAsync(maxAgeRangeYears: 150, minimumMemberAge: 18);
        var svc = BuildMemberService();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            svc.CreateAsync(new CreateMemberRequest
            {
                Name = "Young Person",
                StreetAddress = "1 Youth Ln",
                JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                DateOfBirth = DateTime.UtcNow.Date.AddYears(-10)
            }));

        Assert.Contains("18", ex.Message);
    }

    [Fact]
    public async Task UpdateMember_DobExceedsMaxAgeRange_ThrowsValidationException()
    {
        var svc = BuildMemberService();
        var member = await svc.CreateAsync(new CreateMemberRequest
        {
            Name = "Update Target",
            StreetAddress = "1 Update St",
            JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        await SeedSettingsAsync(maxAgeRangeYears: 100, minimumMemberAge: 0);

        await Assert.ThrowsAsync<ValidationException>(() =>
            svc.UpdateAsync(member.Id, new UpdateMemberRequest
            {
                Name = member.Name,
                StreetAddress = member.StreetAddress,
                JoinDate = member.JoinDate,
                DateOfBirth = DateTime.UtcNow.Date.AddYears(-101)
            }));
    }

    [Fact]
    public async Task UpdateMember_ValidDob_UpdatesSuccessfully()
    {
        var svc = BuildMemberService();
        var member = await svc.CreateAsync(new CreateMemberRequest
        {
            Name = "Update Target",
            StreetAddress = "1 Update St",
            JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        var dob = DateTime.UtcNow.Date.AddYears(-40);
        await svc.UpdateAsync(member.Id, new UpdateMemberRequest
        {
            Name = member.Name,
            StreetAddress = member.StreetAddress,
            JoinDate = member.JoinDate,
            DateOfBirth = dob
        });

        var updated = await svc.GetByIdAsync(member.Id);
        Assert.Equal(dob, updated!.DateOfBirth);
    }

    // --- Status transitions ---

    [Fact]
    public async Task InactivateMember_HiddenFromActiveList()
    {
        var svc = BuildMemberService();

        var member = await svc.CreateAsync(new CreateMemberRequest
        {
            Name = "Carol Chu",
            StreetAddress = "4 Oak Rd",
            JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        await svc.InactivateAsync(member.Id);

        var activeMembers = await svc.GetByStatusAsync(MemberStatus.Active);
        Assert.DoesNotContain(activeMembers, m => m.Id == member.Id);
    }

    [Fact]
    public async Task InactivateMember_AppearsInInactiveList()
    {
        var svc = BuildMemberService();

        var member = await svc.CreateAsync(new CreateMemberRequest
        {
            Name = "Dan Hughes",
            StreetAddress = "5 Pine Ct",
            JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        await svc.InactivateAsync(member.Id);

        var inactiveMembers = await svc.GetByStatusAsync(MemberStatus.Inactive);
        Assert.Contains(inactiveMembers, m => m.Id == member.Id);
    }

    [Fact]
    public async Task ArchiveMember_ExcludedFromAllLists()
    {
        var svc = BuildMemberService();

        var member = await svc.CreateAsync(new CreateMemberRequest
        {
            Name = "Eve White",
            StreetAddress = "6 Cedar Ave",
            JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        await svc.ArchiveAsync(member.Id);

        var active = await svc.GetByStatusAsync(MemberStatus.Active);
        var inactive = await svc.GetByStatusAsync(MemberStatus.Inactive);
        Assert.DoesNotContain(active, m => m.Id == member.Id);
        Assert.DoesNotContain(inactive, m => m.Id == member.Id);
    }

    // --- Committee history ---

    [Fact]
    public async Task CommitteeHistory_CurrentYear_CanBeRetrieved()
    {
        var memberSvc = BuildMemberService();
        var committeeSvc = BuildCommitteeService();

        var member = await memberSvc.CreateAsync(new CreateMemberRequest
        {
            Name = "Frank Stone",
            StreetAddress = "7 Willow Dr",
            JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        await committeeSvc.AddOrUpdateAsync(member.Id, DateTime.UtcNow.Year, "President");

        var history = await committeeSvc.GetHistoryAsync(member.Id);
        Assert.Single(history);
        Assert.Equal(DateTime.UtcNow.Year, history[0].Year);
        Assert.Equal("President", history[0].Position);
    }

    [Fact]
    public async Task ArchiveMember_CascadesSoftDelete_ToCurrentYearCommittee()
    {
        var memberSvc = BuildMemberService();
        var committeeSvc = BuildCommitteeService();

        var member = await memberSvc.CreateAsync(new CreateMemberRequest
        {
            Name = "Grace Lee",
            StreetAddress = "8 Birch Blvd",
            JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        await committeeSvc.AddOrUpdateAsync(member.Id, DateTime.UtcNow.Year, "Treasurer");
        await memberSvc.ArchiveAsync(member.Id);

        // After archive, the current-year committee record should be soft-deleted
        var committeeRepo = new CommitteeMembershipRepository(_db);
        var active = await committeeRepo.GetByMemberAsync(member.Id);
        Assert.Empty(active); // soft-deleted records filtered out

        var archived = await committeeRepo.GetArchivedAsync();
        Assert.Contains(archived, c => c.MemberId == member.Id && c.Year == DateTime.UtcNow.Year);
    }

    // --- Helpers ---

    private MemberService BuildMemberService()
    {
        var memberRepo = new MemberRepository(_db);
        var committeeRepo = new CommitteeMembershipRepository(_db);
        var settingsRepo = new SettingsRepository(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditSvc = new AuditTrailService(
            auditRepo, NullLogger<AuditTrailService>.Instance);
        var ageCalc = new AgeCalculationService();
        var validation = new MemberValidationService(ageCalc);
        var unitOfWork = new UnitOfWork(_db);

        return new MemberService(memberRepo, committeeRepo, validation, settingsRepo, auditSvc, unitOfWork);
    }

    private async Task SeedSettingsAsync(int maxAgeRangeYears, int minimumMemberAge)
    {
        var settingsRepo = new SettingsRepository(_db);
        await settingsRepo.SaveAsync(new StageFright.Core.Entities.Settings
        {
            Id = Guid.NewGuid(),
            OrganizationName = "Test Org",
            AnnualFee = 50m,
            AttendanceFee = 5m,
            MembershipRenewalMonth = 1,
            MaxAgeRangeYears = maxAgeRangeYears,
            MinimumMemberAge = minimumMemberAge
        });
    }

    private CommitteeService BuildCommitteeService()
    {
        var committeeRepo = new CommitteeMembershipRepository(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditSvc = new AuditTrailService(
            auditRepo, NullLogger<AuditTrailService>.Instance);
        var unitOfWork = new UnitOfWork(_db);

        return new CommitteeService(committeeRepo, auditSvc, unitOfWork);
    }
}
