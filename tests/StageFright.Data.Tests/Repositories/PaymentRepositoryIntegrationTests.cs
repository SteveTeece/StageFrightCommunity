using Microsoft.EntityFrameworkCore;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for PaymentRepository against a real SQLite in-memory database.
/// Verifies UpdateNotesAsync correctness: bumps UpdatedAt, audits old/new,
/// and confirms no other mutation path exists on the repository.
/// </summary>
public sealed class PaymentRepositoryIntegrationTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;
    private PaymentRepository _sut = null!;
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        _sut = new PaymentRepository(_db, _audit);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- AddAsync ---

    [Fact]
    public async Task AddAsync_PersistsAndReturnsPayment()
    {
        var memberId = await SeedMemberAsync();
        var payment = MakePayment(memberId);

        var saved = await _sut.AddAsync(payment);

        Assert.Equal(payment.Id, saved.Id);
        var fromDb = await _sut.GetByIdAsync(payment.Id);
        Assert.NotNull(fromDb);
        Assert.Equal(payment.Amount, fromDb!.Amount);
        Assert.Equal(payment.PaymentMethod, fromDb.PaymentMethod);
    }

    // --- GetByIdAsync ---

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // --- GetByMemberAsync ---

    [Fact]
    public async Task GetByMemberAsync_ReturnsPaymentsForMember_OrderedDescByDate()
    {
        var memberId = await SeedMemberAsync();

        var p1 = MakePayment(memberId, date: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var p2 = MakePayment(memberId, date: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        await _sut.AddAsync(p1);
        await _sut.AddAsync(p2);

        var results = await _sut.GetByMemberAsync(memberId);

        Assert.Equal(2, results.Count);
        // OrderByDescending date: p2 first
        Assert.Equal(p2.Id, results[0].Id);
    }

    [Fact]
    public async Task GetByMemberAsync_ExcludesOtherMembers()
    {
        var member1 = await SeedMemberAsync();
        var member2 = await SeedMemberAsync();

        await _sut.AddAsync(MakePayment(member1));
        await _sut.AddAsync(MakePayment(member2));

        var results = await _sut.GetByMemberAsync(member1);

        Assert.Single(results);
        Assert.Equal(member1, results[0].MemberId);
    }

    // --- UpdateNotesAsync ---

    [Fact]
    public async Task UpdateNotesAsync_UpdatesNotesField()
    {
        var memberId = await SeedMemberAsync();
        var payment = await _sut.AddAsync(MakePayment(memberId, notes: "original"));

        await _sut.UpdateNotesAsync(payment.Id, "updated note");

        var fromDb = await _sut.GetByIdAsync(payment.Id);
        Assert.Equal("updated note", fromDb!.Notes);
    }

    [Fact]
    public async Task UpdateNotesAsync_BumpsUpdatedAt()
    {
        var memberId = await SeedMemberAsync();
        var payment = await _sut.AddAsync(MakePayment(memberId));
        var originalUpdatedAt = payment.UpdatedAt;

        // Wait a tick so timestamp differs
        await Task.Delay(10);

        await _sut.UpdateNotesAsync(payment.Id, "new note");

        var fromDb = await _sut.GetByIdAsync(payment.Id);
        Assert.True(fromDb!.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task UpdateNotesAsync_AcceptsNull_ClearingNotes()
    {
        var memberId = await SeedMemberAsync();
        var payment = await _sut.AddAsync(MakePayment(memberId, notes: "some note"));

        await _sut.UpdateNotesAsync(payment.Id, null);

        var fromDb = await _sut.GetByIdAsync(payment.Id);
        Assert.Null(fromDb!.Notes);
    }

    [Fact]
    public async Task UpdateNotesAsync_ThrowsEntityNotFound_ForUnknownId()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _sut.UpdateNotesAsync(Guid.NewGuid(), "note"));
    }

    [Fact]
    public async Task UpdateNotesAsync_CallsAuditService_WithOldAndNewNotes()
    {
        var memberId = await SeedMemberAsync();
        var payment = await _sut.AddAsync(MakePayment(memberId, notes: "old note"));

        await _sut.UpdateNotesAsync(payment.Id, "new note");

        await _audit.Received(1).LogAsync(
            nameof(Payment), payment.Id, AuditAction.Update,
            "old note", "new note",
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImmutableFields_Amount_CannotBeChangedViaRepository()
    {
        // The repository exposes NO UpdateAsync for Payment — immutability enforced at interface level.
        // Verify IPaymentRepository does NOT implement an UpdateAsync that would modify Amount.
        var repoType = typeof(PaymentRepository);
        var updateAsyncMethod = repoType.GetMethod("UpdateAsync");
        Assert.Null(updateAsyncMethod);
    }

    // --- Helpers ---

    private async Task<Guid> SeedMemberAsync()
    {
        var member = new Member
        {
            Id = Guid.NewGuid(), Name = "Test Member", StreetAddress = "1 Test St",
            Status = MemberStatus.Active,
            ActivateDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            JoinDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync();
        return member.Id;
    }

    private static Payment MakePayment(Guid memberId, DateTime? date = null, string? notes = null) => new()
    {
        Id = Guid.NewGuid(),
        MemberId = memberId,
        Date = date ?? new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        Amount = 50m,
        PaymentMethod = PaymentMethod.Cash,
        PaymentType = PaymentType.Annual,
        Notes = notes,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
