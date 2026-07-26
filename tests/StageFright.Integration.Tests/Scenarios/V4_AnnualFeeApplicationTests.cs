using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V4: annual fee batch application.
/// Verifies that only eligible active members are billed, inactive members are skipped,
/// members with an existing current-year annual fee are skipped, GL pairs are created,
/// and the Finance GL balance reflects the applied fees.
/// Uses a real SQLite in-memory database with full EF migrations.
/// </summary>
public sealed class V4_AnnualFeeApplicationTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    // System account GUIDs (seeded by StageFrightDbContext)
    private static readonly Guid MemberReceivableAccountId = new("00000000-0000-0000-0000-000000000002");

    private static readonly Guid IncomeAccountId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        // Seed income account for GL pairs
        _db.Accounts.Add(new Account
        {
            Id = IncomeAccountId,
            Name = "Membership Income",
            Type = AccountType.Income,
            AccountNumber = "1000",
            SortOrder = 0,
            IsSystem = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Seed settings
        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(),
            OrganizationName = "Test Choir",
            AnnualFee = 50m,
            AttendanceFee = 10m,
            MembershipRenewalMonth = 6,
            CommitteeRenewalMonth = 1,
            MaxAgeRangeYears = 150,
            MinimumMemberAge = 0,
            Theme = Theme.Light,
            SchemaVersion = "1.0.0",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- GetEligibleMembersAsync ---

    [Fact]
    public async Task GetEligibleMembers_ReturnsOnlyActiveMembers_WithNoCurrentYearAnnualFee()
    {
        var active1 = await AddActiveMember("Alice");
        var active2 = await AddActiveMember("Bob");
        await AddInactiveMember("Charlie");

        var svc = BuildFeeService();
        var eligible = await svc.GetEligibleMembersAsync();

        Assert.Equal(2, eligible.Count);
        Assert.All(eligible, m => Assert.Equal(MemberStatus.Active, m.Status));
    }

    [Fact]
    public async Task GetEligibleMembers_ExcludesMember_WithExistingAnnualFeeThisYear()
    {
        var active1 = await AddActiveMember("Alice");
        var active2 = await AddActiveMember("Bob");

        // Alice already has a current-year annual fee (unpaid)
        await AddAnnualFeeForMember(active1.Id, paidAtCreation: false);

        var svc = BuildFeeService();
        var eligible = await svc.GetEligibleMembersAsync();

        Assert.Single(eligible);
        Assert.Equal(active2.Id, eligible[0].Id);
    }

    [Fact]
    public async Task GetEligibleMembers_ExcludesMember_WithPaidAnnualFeeThisYear()
    {
        var active = await AddActiveMember("Diana");

        // Paid annual fee also blocks re-billing
        await AddAnnualFeeForMember(active.Id, paidAtCreation: true);

        var svc = BuildFeeService();
        var eligible = await svc.GetEligibleMembersAsync();

        Assert.Empty(eligible);
    }

    [Fact]
    public async Task GetEligibleMembers_IncludesMember_WithAnnualFeeFromPriorYear()
    {
        var active = await AddActiveMember("Eve");

        // Prior year fee should NOT block current year billing
        await AddAnnualFeeForMemberInYear(active.Id, DateTime.UtcNow.Year - 1);

        var svc = BuildFeeService();
        var eligible = await svc.GetEligibleMembersAsync();

        Assert.Single(eligible);
    }

    // --- ApplyAnnualFeesAsync ---

    [Fact]
    public async Task ApplyAnnualFees_ActiveEligibleMember_CreatesFeeAndGLPair()
    {
        var member = await AddActiveMember("Frank");
        var svc = BuildFeeService();

        var count = await svc.ApplyAnnualFeesAsync(new[] { member.Id });

        Assert.Equal(1, count);

        // Fee created
        var fee = await _db.Fees.FirstOrDefaultAsync(f => f.MemberId == member.Id);
        Assert.NotNull(fee);
        Assert.Equal(FeeType.Annual, fee!.FeeType);
        Assert.Equal(50m, fee.Amount);
        Assert.False(fee.PaidAtCreation);
        Assert.Equal(DateTime.UtcNow.Year, fee.FeeDate.Year);
        Assert.Equal(1, fee.FeeDate.Month);
        Assert.Equal(1, fee.FeeDate.Day);
        Assert.Equal(12, fee.DueDate.Month);
        Assert.Equal(31, fee.DueDate.Day);

        // GL pair: debit MemberReceivable (member-specific), credit Income (org-level, MemberId=null)
        var allTransactions = await _db.Transactions.ToListAsync();
        Assert.Equal(2, allTransactions.Count);

        Assert.Contains(allTransactions, t =>
            t.DebitAmount == 50m && t.AccountId == MemberReceivableAccountId && t.MemberId == member.Id);
        Assert.Contains(allTransactions, t =>
            t.CreditAmount == 50m && t.GLAccount == "1000" && t.MemberId == null);
    }

    [Fact]
    public async Task ApplyAnnualFees_MultipleMembersAtOnce_CreatesFeeForAll()
    {
        var active1 = await AddActiveMember("George");
        var active2 = await AddActiveMember("Harriet");

        var svc = BuildFeeService();
        var count = await svc.ApplyAnnualFeesAsync(new[] { active1.Id, active2.Id });

        Assert.Equal(2, count);
        Assert.Equal(2, await _db.Fees.CountAsync());
        // 2 GL pairs = 4 transactions total (2 MemberReceivable debits + 2 Income credits)
        Assert.Equal(4, await _db.Transactions.CountAsync());
    }

    [Fact]
    public async Task ApplyAnnualFees_InactiveMember_NotInEligibleList_NeverCharged()
    {
        await AddActiveMember("Ian");
        var inactive = await AddInactiveMember("Janet");

        var svc = BuildFeeService();
        var eligible = await svc.GetEligibleMembersAsync();

        // Only Ian is eligible; Janet is not in the list
        Assert.DoesNotContain(eligible, m => m.Id == inactive.Id);

        var count = await svc.ApplyAnnualFeesAsync(eligible.Select(m => m.Id).ToList());
        Assert.Equal(1, count);

        // Janet has no fee
        Assert.False(await _db.Fees.AnyAsync(f => f.MemberId == inactive.Id));
    }

    [Fact]
    public async Task ApplyAnnualFees_FinanceTileBalance_IncreasesAfterApplication()
    {
        var member = await AddActiveMember("Karen");
        var svc = BuildFeeService();

        var glRepo = new GLRepository(_db);
        var balanceBefore = await glRepo.GetTotalOutstandingAsync();

        await svc.ApplyAnnualFeesAsync(new[] { member.Id });

        var balanceAfter = await glRepo.GetTotalOutstandingAsync();

        // MemberReceivable debit increases outstanding balance by 50
        Assert.Equal(balanceBefore + 50m, balanceAfter);
    }

    [Fact]
    public async Task ApplyAnnualFees_EmptyList_ReturnsZeroAndCreatesNoFees()
    {
        var svc = BuildFeeService();

        var count = await svc.ApplyAnnualFeesAsync(Array.Empty<Guid>());

        Assert.Equal(0, count);
        Assert.Equal(0, await _db.Fees.CountAsync());
    }

    // --- Helpers ---

    private FeeService BuildFeeService()
    {
        var memberRepo = new MemberRepository(_db);
        var feeRepo = new FeeRepository(_db);
        var glRepo = new GLRepository(_db);
        var accountRepo = new AccountRepository(_db);
        var settingsRepo = new SettingsRepository(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditSvc = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var unitOfWork = new UnitOfWork(_db);

        return new FeeService(memberRepo, feeRepo, glRepo, accountRepo, settingsRepo, auditSvc, unitOfWork);
    }

    private async Task<Member> AddActiveMember(string name)
    {
        var member = new Member
        {
            Id = Guid.NewGuid(), FirstName = name, StreetAddress = "1 Test St",
            Status = MemberStatus.Active,
            ActivateDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            JoinDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync();
        return member;
    }

    private async Task<Member> AddInactiveMember(string name)
    {
        var member = new Member
        {
            Id = Guid.NewGuid(), FirstName = name, StreetAddress = "2 Test St",
            Status = MemberStatus.Inactive,
            InactivateDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            JoinDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync();
        return member;
    }

    private async Task AddAnnualFeeForMember(Guid memberId, bool paidAtCreation)
    {
        await AddAnnualFeeForMemberInYear(memberId, DateTime.UtcNow.Year, paidAtCreation);
    }

    private async Task AddAnnualFeeForMemberInYear(Guid memberId, int year, bool paidAtCreation = false)
    {
        _db.Fees.Add(new Fee
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            FeeType = FeeType.Annual,
            Amount = 50m,
            FeeDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(year, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            PaidAtCreation = paidAtCreation,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
