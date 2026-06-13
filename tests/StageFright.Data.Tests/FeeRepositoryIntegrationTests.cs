using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Data.Tests;

/// <summary>
/// Integration tests for FeeRepository against a real SQLite in-memory database.
/// Verifies AnnualFeeExistsAsync and AttendanceFeeExistsAsync idempotency behaviour.
/// </summary>
public sealed class FeeRepositoryIntegrationTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;
    private FeeRepository _sut = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        _sut = new FeeRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- AnnualFeeExistsAsync ---

    [Fact]
    public async Task AnnualFeeExistsAsync_ReturnsTrue_WhenFeeExistsForYear()
    {
        var memberId = await SeedMemberAsync();
        var year = 2026;

        await AddFeeAsync(memberId, FeeType.Annual,
            new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(year, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            paidAtCreation: false);

        var exists = await _sut.AnnualFeeExistsAsync(memberId, year);

        Assert.True(exists);
    }

    [Fact]
    public async Task AnnualFeeExistsAsync_ReturnsFalse_WhenNoFeeForYear()
    {
        var memberId = await SeedMemberAsync();

        var exists = await _sut.AnnualFeeExistsAsync(memberId, 2026);

        Assert.False(exists);
    }

    [Fact]
    public async Task AnnualFeeExistsAsync_ReturnsTrue_WhenFeeIsPaid()
    {
        var memberId = await SeedMemberAsync();
        var year = 2026;

        await AddFeeAsync(memberId, FeeType.Annual,
            new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(year, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            paidAtCreation: true);

        // Paid or unpaid — both should return true
        var exists = await _sut.AnnualFeeExistsAsync(memberId, year);

        Assert.True(exists);
    }

    [Fact]
    public async Task AnnualFeeExistsAsync_ReturnsFalse_WhenFeeIsForDifferentYear()
    {
        var memberId = await SeedMemberAsync();

        await AddFeeAsync(memberId, FeeType.Annual,
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            paidAtCreation: false);

        // Query for 2026 — should not find the 2025 fee
        var exists = await _sut.AnnualFeeExistsAsync(memberId, 2026);

        Assert.False(exists);
    }

    [Fact]
    public async Task AnnualFeeExistsAsync_ReturnsFalse_WhenOnlyAttendanceFeeExists()
    {
        var memberId = await SeedMemberAsync();
        var year = 2026;

        await AddFeeAsync(memberId, FeeType.Attendance,
            new DateTime(year, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(year, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            paidAtCreation: true);

        var exists = await _sut.AnnualFeeExistsAsync(memberId, year);

        Assert.False(exists);
    }

    // --- AttendanceFeeExistsAsync ---

    [Fact]
    public async Task AttendanceFeeExistsAsync_ReturnsTrue_WhenFeeExistsForRehearsal()
    {
        var memberId = await SeedMemberAsync();
        var rehearsalId = await SeedRehearsalAsync();

        await AddAttendanceFeeAsync(memberId, rehearsalId);

        var exists = await _sut.AttendanceFeeExistsAsync(memberId, rehearsalId);

        Assert.True(exists);
    }

    [Fact]
    public async Task AttendanceFeeExistsAsync_ReturnsFalse_WhenNoFeeForRehearsal()
    {
        var memberId = await SeedMemberAsync();
        var rehearsalId = Guid.NewGuid();

        var exists = await _sut.AttendanceFeeExistsAsync(memberId, rehearsalId);

        Assert.False(exists);
    }

    [Fact]
    public async Task AttendanceFeeExistsAsync_ReturnsFalse_WhenFeeIsForDifferentRehearsal()
    {
        var memberId = await SeedMemberAsync();
        var rehearsal1Id = await SeedRehearsalAsync();
        var rehearsal2Id = await SeedRehearsalAsync();

        await AddAttendanceFeeAsync(memberId, rehearsal1Id);

        var exists = await _sut.AttendanceFeeExistsAsync(memberId, rehearsal2Id);

        Assert.False(exists);
    }

    // --- AddAsync ---

    [Fact]
    public async Task AddAsync_PersistsAndReturnsFee()
    {
        var memberId = await SeedMemberAsync();

        var fee = new Fee
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            FeeType = FeeType.Annual,
            Amount = 50m,
            FeeDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            PaidAtCreation = false,
            CreatedAt = DateTime.UtcNow
        };

        var saved = await _sut.AddAsync(fee);

        Assert.Equal(fee.Id, saved.Id);
        var fromDb = await _sut.GetByIdAsync(fee.Id);
        Assert.NotNull(fromDb);
        Assert.Equal(FeeType.Annual, fromDb.FeeType);
        Assert.Equal(50m, fromDb.Amount);
    }

    // --- Helpers ---

    private async Task<Guid> SeedMemberAsync()
    {
        var member = new Member
        {
            Id = Guid.NewGuid(), Name = "Test Member",
            StreetAddress = "1 Test St",
            Status = MemberStatus.Active,
            JoinDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ActivateDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync();
        return member.Id;
    }

    private async Task<Guid> SeedRehearsalAsync()
    {
        var rehearsal = new Rehearsal
        {
            Id = Guid.NewGuid(),
            Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Time = TimeSpan.FromHours(19),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Rehearsals.Add(rehearsal);
        await _db.SaveChangesAsync();
        return rehearsal.Id;
    }

    private async Task AddFeeAsync(Guid memberId, FeeType type, DateTime feeDate, DateTime dueDate, bool paidAtCreation)
    {
        _db.Fees.Add(new Fee
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            FeeType = type,
            Amount = 50m,
            FeeDate = feeDate,
            DueDate = dueDate,
            PaidAtCreation = paidAtCreation,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private async Task AddAttendanceFeeAsync(Guid memberId, Guid rehearsalId)
    {
        _db.Fees.Add(new Fee
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            FeeType = FeeType.Attendance,
            Amount = 10m,
            FeeDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            PaidAtCreation = true,
            RehearsalId = rehearsalId,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
