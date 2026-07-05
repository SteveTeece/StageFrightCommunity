using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for FeeService — annual fee eligibility, GL pair creation, inactive skip,
/// existing-fee skip (paid or unpaid), and batch atomicity.
/// </summary>
public class FeeServiceTests : TestBase
{
    private readonly IMemberRepository _memberRepo = Substitute.For<IMemberRepository>();
    private readonly IFeeRepository _feeRepo = Substitute.For<IFeeRepository>();
    private readonly IGLRepository _glRepo = Substitute.For<IGLRepository>();
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly ISettingsRepository _settingsRepo = Substitute.For<ISettingsRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static readonly Guid ActiveMember1Id = Guid.NewGuid();
    private static readonly Guid ActiveMember2Id = Guid.NewGuid();
    private static readonly Guid InactiveMemberId = Guid.NewGuid();
    private static readonly Guid IncomeAccountId = Guid.NewGuid();

    private static readonly Guid MemberReceivableAccountId = new("00000000-0000-0000-0000-000000000002");

    private readonly FeeService _sut;

    public FeeServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new Settings
            {
                Id = Guid.NewGuid(), OrganizationName = "Test Choir",
                AnnualFee = 50m, AttendanceFee = 10m,
                MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
                MinimumMemberAge = 0, SchemaVersion = "1.0.0",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        var incomeAccount = new Account
        {
            Id = IncomeAccountId, Name = "Membership Income",
            Type = AccountType.Income, AccountNumber = "4000",
            IsSystem = false, SortOrder = 0,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account> { incomeAccount });

        _memberRepo.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(new List<Member>
            {
                ActiveMember(ActiveMember1Id, "Alice"),
                ActiveMember(ActiveMember2Id, "Bob")
            });

        // Default: no existing annual fees
        _feeRepo.AnnualFeeExistsAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _feeRepo.AddAsync(Arg.Any<Fee>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Fee>(0));

        _sut = new FeeService(
            _memberRepo, _feeRepo, _glRepo, _accountRepo, _settingsRepo, _audit, _unitOfWork);
    }

    // --- GetEligibleMembersAsync ---

    [Fact]
    public async Task GetEligibleMembers_ReturnsActiveMembers_WithNoExistingAnnualFee()
    {
        var result = await _sut.GetEligibleMembersAsync(Ct);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetEligibleMembers_ExcludesMembers_WithExistingAnnualFeeThisYear()
    {
        // Member1 already has an annual fee this year
        _feeRepo.AnnualFeeExistsAsync(ActiveMember1Id, DateTime.UtcNow.Year, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _sut.GetEligibleMembersAsync(Ct);

        Assert.Single(result);
        Assert.Equal(ActiveMember2Id, result[0].Id);
    }

    [Fact]
    public async Task GetEligibleMembers_InactiveMembers_NeverIncluded()
    {
        // Even if inactive member has no fee, they are never returned
        // because GetByStatusAsync only returns Active members
        _memberRepo.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { ActiveMember(ActiveMember1Id, "Alice") });

        var result = await _sut.GetEligibleMembersAsync(Ct);

        Assert.Single(result);
        Assert.Equal(ActiveMember1Id, result[0].Id);
    }

    // --- ApplyAnnualFeesAsync ---

    [Fact]
    public async Task ApplyAnnualFees_EligibleMember_CreatesFeeAndGLPair()
    {
        var count = await _sut.ApplyAnnualFeesAsync(new[] { ActiveMember1Id }, Ct);

        Assert.Equal(1, count);

        await _feeRepo.Received(1).AddAsync(
            Arg.Is<Fee>(f =>
                f.MemberId == ActiveMember1Id &&
                f.FeeType == FeeType.Annual &&
                f.Amount == 50m &&
                f.PaidAtCreation == false),
            Arg.Any<CancellationToken>());

        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 50m && t.GLAccount == "1200"),
            Arg.Is<Transaction>(t => t.CreditAmount == 50m && t.GLAccount == "4000"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyAnnualFees_FeeDate_IsJan1OfCurrentYear()
    {
        await _sut.ApplyAnnualFeesAsync(new[] { ActiveMember1Id }, Ct);

        await _feeRepo.Received(1).AddAsync(
            Arg.Is<Fee>(f =>
                f.FeeDate.Month == 1 &&
                f.FeeDate.Day == 1 &&
                f.FeeDate.Year == DateTime.UtcNow.Year),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyAnnualFees_DueDate_IsDec31OfCurrentYear()
    {
        await _sut.ApplyAnnualFeesAsync(new[] { ActiveMember1Id }, Ct);

        await _feeRepo.Received(1).AddAsync(
            Arg.Is<Fee>(f =>
                f.DueDate.Month == 12 &&
                f.DueDate.Day == 31 &&
                f.DueDate.Year == DateTime.UtcNow.Year),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyAnnualFees_MultipleMembersBatch_CreatesFeesForAll()
    {
        var count = await _sut.ApplyAnnualFeesAsync(
            new[] { ActiveMember1Id, ActiveMember2Id }, Ct);

        Assert.Equal(2, count);
        await _feeRepo.Received(2).AddAsync(Arg.Any<Fee>(), Arg.Any<CancellationToken>());
        await _glRepo.Received(2).AddPairAsync(
            Arg.Any<Transaction>(), Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyAnnualFees_WritesAuditEntry_ForEachMember()
    {
        await _sut.ApplyAnnualFeesAsync(new[] { ActiveMember1Id, ActiveMember2Id }, Ct);

        await _audit.Received(2).LogAsync(
            nameof(Fee), Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(),
            "system", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyAnnualFees_RunsInsideUnitOfWork_Transaction()
    {
        await _sut.ApplyAnnualFeesAsync(new[] { ActiveMember1Id }, Ct);

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyAnnualFees_EmptyList_ReturnsZero()
    {
        var count = await _sut.ApplyAnnualFeesAsync(Array.Empty<Guid>(), Ct);

        Assert.Equal(0, count);
        await _feeRepo.DidNotReceive().AddAsync(Arg.Any<Fee>(), Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private static Member ActiveMember(Guid id, string name) => new()
    {
        Id = id, Name = name,
        StreetAddress = "1 Test St",
        Status = MemberStatus.Active,
        JoinDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ActivateDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
