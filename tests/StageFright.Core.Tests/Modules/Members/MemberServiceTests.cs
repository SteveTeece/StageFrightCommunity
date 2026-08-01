using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Members;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Members;

/// <summary>
/// Unit tests for MemberService — status transitions, audit trail, and cascade behavior.
/// </summary>
public class MemberServiceTests : TestBase
{
    private readonly IMemberRepository _memberRepo = Substitute.For<IMemberRepository>();
    private readonly ICommitteePositionRecordRepository _committeeRepo = Substitute.For<ICommitteePositionRecordRepository>();
    private readonly ISettingsRepository _settingsRepo = Substitute.For<ISettingsRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly MemberValidationService _validation;
    private readonly AgeCalculationService _ageCalc = new();

    public MemberServiceTests()
    {
        _validation = new MemberValidationService(_ageCalc);

        // UnitOfWork just executes the operation inline for unit tests
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        // Default settings: no age constraints
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new Settings
            {
                Id = Guid.NewGuid(), OrganizationName = "Test",
                AnnualFee = 50m, AttendanceFee = 5m,
                MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
                MinimumMemberAge = 0, SchemaVersion = "1.0.0",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        _memberRepo.AddAsync(Arg.Any<Member>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Member>(0));
    }

    private MemberService CreateService() =>
        new(_memberRepo, _committeeRepo, _validation, _settingsRepo, _audit, _unitOfWork);

    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_Returns_ActiveMember_With_ActivateDate()
    {
        var svc = CreateService();
        var request = ValidRequest();

        var member = await svc.CreateAsync(request, Ct);

        Assert.Equal(MemberStatus.Active, member.Status);
        Assert.NotNull(member.ActivateDate);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenFirstNameIsEmpty()
    {
        var svc = CreateService();
        var request = ValidRequest() with { FirstName = "" };

        await Assert.ThrowsAsync<ValidationException>(() => svc.CreateAsync(request, Ct));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenLastNameIsEmpty()
    {
        var svc = CreateService();
        var request = ValidRequest() with { LastName = "" };

        await Assert.ThrowsAsync<ValidationException>(() => svc.CreateAsync(request, Ct));
    }

    [Fact]
    public async Task CreateAsync_Trims_And_Maps_FirstNameAndLastName_Independently()
    {
        var svc = CreateService();
        var request = ValidRequest() with { FirstName = "  Jane  ", LastName = "  Doe  " };

        var member = await svc.CreateAsync(request, Ct);

        Assert.Equal("Jane", member.FirstName);
        Assert.Equal("Doe", member.LastName);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenAddressIsEmpty()
    {
        var svc = CreateService();
        var request = ValidRequest() with { StreetAddress = "" };

        await Assert.ThrowsAsync<ValidationException>(() => svc.CreateAsync(request, Ct));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenEmailFormatInvalid()
    {
        var svc = CreateService();
        var request = ValidRequest() with { Email = "not-an-email" };

        await Assert.ThrowsAsync<ValidationException>(() => svc.CreateAsync(request, Ct));
    }

    [Fact]
    public async Task CreateAsync_WritesAuditEntry()
    {
        var svc = CreateService();

        await svc.CreateAsync(ValidRequest(), Ct);

        await _audit.Received(1).LogAsync(
            "Member", Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- UpdateAsync ---

    [Fact]
    public async Task UpdateAsync_Trims_And_Maps_FirstNameAndLastName_Independently()
    {
        var member = ActiveMember();
        _memberRepo.GetByIdAsync(member.Id, Arg.Any<CancellationToken>()).Returns(member);
        var svc = CreateService();
        var request = ValidUpdateRequest() with { FirstName = "  Janet  ", LastName = "  Smith  " };

        await svc.UpdateAsync(member.Id, request, Ct);

        await _memberRepo.Received(1).UpdateAsync(
            Arg.Is<Member>(m => m.FirstName == "Janet" && m.LastName == "Smith"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WritesAuditEntry_WithOldAndNewNameValues()
    {
        var member = ActiveMember();
        var oldFirstName = member.FirstName;
        var oldLastName = member.LastName;
        _memberRepo.GetByIdAsync(member.Id, Arg.Any<CancellationToken>()).Returns(member);
        var svc = CreateService();
        var request = ValidUpdateRequest() with { FirstName = "Janet", LastName = "Smith" };

        await svc.UpdateAsync(member.Id, request, Ct);

        await _audit.Received(1).LogAsync(
            "Member", member.Id, AuditAction.Update,
            $"{oldFirstName} {oldLastName}", "Janet Smith",
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenMemberNotFound()
    {
        _memberRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Member?)null);
        var svc = CreateService();

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => svc.UpdateAsync(Guid.NewGuid(), ValidUpdateRequest(), Ct));
    }

    // --- InactivateAsync ---

    [Fact]
    public async Task InactivateAsync_SetsInactivateDateAndStatus()
    {
        var member = ActiveMember();
        _memberRepo.GetByIdAsync(member.Id, Arg.Any<CancellationToken>()).Returns(member);
        var svc = CreateService();

        await svc.InactivateAsync(member.Id, Ct);

        await _memberRepo.Received(1).UpdateAsync(
            Arg.Is<Member>(m => m.Status == MemberStatus.Inactive && m.InactivateDate != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InactivateAsync_WritesAuditEntry()
    {
        var member = ActiveMember();
        _memberRepo.GetByIdAsync(member.Id, Arg.Any<CancellationToken>()).Returns(member);
        var svc = CreateService();

        await svc.InactivateAsync(member.Id, Ct);

        await _audit.Received(1).LogAsync(
            "Member", member.Id, AuditAction.StatusChange,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InactivateAsync_DoesNot_CascadeToCommitteePositionRecords()
    {
        var member = ActiveMember();
        _memberRepo.GetByIdAsync(member.Id, Arg.Any<CancellationToken>()).Returns(member);
        var svc = CreateService();

        await svc.InactivateAsync(member.Id, Ct);

        // CommitteePositionRecord records must NOT be touched on inactivation
        await _committeeRepo.DidNotReceive().ArchiveAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InactivateAsync_Throws_WhenMemberNotFound()
    {
        _memberRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Member?)null);
        var svc = CreateService();

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => svc.InactivateAsync(Guid.NewGuid(), Ct));
    }

    // --- ActivateAsync ---

    [Fact]
    public async Task ActivateAsync_SetsActivateDateAndStatus()
    {
        var member = InactiveMember();
        _memberRepo.GetByIdAsync(member.Id, Arg.Any<CancellationToken>()).Returns(member);
        var svc = CreateService();

        await svc.ActivateAsync(member.Id, Ct);

        await _memberRepo.Received(1).UpdateAsync(
            Arg.Is<Member>(m => m.Status == MemberStatus.Active && m.ActivateDate != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActivateAsync_WritesAuditEntry()
    {
        var member = InactiveMember();
        _memberRepo.GetByIdAsync(member.Id, Arg.Any<CancellationToken>()).Returns(member);
        var svc = CreateService();

        await svc.ActivateAsync(member.Id, Ct);

        await _audit.Received(1).LogAsync(
            "Member", member.Id, AuditAction.StatusChange,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- ArchiveAsync ---

    [Fact]
    public async Task ArchiveAsync_SoftDeletesMember()
    {
        var member = ActiveMember();
        _memberRepo.GetByIdAsync(member.Id, Arg.Any<CancellationToken>()).Returns(member);
        var svc = CreateService();

        await svc.ArchiveAsync(member.Id, Ct);

        await _memberRepo.Received(1).ArchiveAsync(member.Id, "system", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveAsync_CascadesSoftDelete_ToCurrentYearCommittee()
    {
        var member = ActiveMember();
        _memberRepo.GetByIdAsync(member.Id, Arg.Any<CancellationToken>()).Returns(member);

        var currentYear = DateTime.UtcNow.Year;
        var committeeRecord = new CommitteePositionRecord
        {
            Id = Guid.NewGuid(), MemberId = member.Id,
            Year = currentYear, Position = "President",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _committeeRepo.GetByMemberAsync(member.Id, Arg.Any<CancellationToken>())
            .Returns(new List<CommitteePositionRecord> { committeeRecord });

        var svc = CreateService();

        await svc.ArchiveAsync(member.Id, Ct);

        await _committeeRepo.Received(1).ArchiveAsync(
            committeeRecord.Id, "system", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveAsync_DoesNotArchive_HistoricalCommitteeRecords()
    {
        var member = ActiveMember();
        _memberRepo.GetByIdAsync(member.Id, Arg.Any<CancellationToken>()).Returns(member);

        // Historical record from last year
        var lastYear = DateTime.UtcNow.Year - 1;
        var historicalRecord = new CommitteePositionRecord
        {
            Id = Guid.NewGuid(), MemberId = member.Id,
            Year = lastYear, Position = "Treasurer",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _committeeRepo.GetByMemberAsync(member.Id, Arg.Any<CancellationToken>())
            .Returns(new List<CommitteePositionRecord> { historicalRecord });

        var svc = CreateService();

        await svc.ArchiveAsync(member.Id, Ct);

        // Historical records should NOT be archived
        await _committeeRepo.DidNotReceive().ArchiveAsync(
            historicalRecord.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveAsync_WritesAuditEntry()
    {
        var member = ActiveMember();
        _memberRepo.GetByIdAsync(member.Id, Arg.Any<CancellationToken>()).Returns(member);
        _committeeRepo.GetByMemberAsync(member.Id, Arg.Any<CancellationToken>())
            .Returns(new List<CommitteePositionRecord>());
        var svc = CreateService();

        await svc.ArchiveAsync(member.Id, Ct);

        await _audit.Received(1).LogAsync(
            "Member", member.Id, AuditAction.Delete,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- GetByStatusAsync ---

    [Fact]
    public async Task GetByStatusAsync_DelegatesToRepository()
    {
        _memberRepo.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { ActiveMember() });
        var svc = CreateService();

        var result = await svc.GetByStatusAsync(MemberStatus.Active, Ct);

        Assert.Single(result);
    }

    // --- Helpers ---

    private static CreateMemberRequest ValidRequest() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        StreetAddress = "1 Main St",
        JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static UpdateMemberRequest ValidUpdateRequest() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        StreetAddress = "1 Main St",
        JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static Member ActiveMember() => new()
    {
        Id = Guid.NewGuid(), FirstName = "Active", LastName = "Member",
        StreetAddress = "1 Test St", Status = MemberStatus.Active,
        ActivateDate = DateTime.UtcNow.Date,
        JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Member InactiveMember() => new()
    {
        Id = Guid.NewGuid(), FirstName = "Inactive", LastName = "Member",
        StreetAddress = "1 Test St", Status = MemberStatus.Inactive,
        InactivateDate = DateTime.UtcNow.Date,
        JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
