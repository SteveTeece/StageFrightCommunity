using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Members;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Members;

/// <summary>
/// Unit tests for CommitteeService — legacy year/position add-update and term-scoped queries.
/// </summary>
public class CommitteeServiceTests : TestBase
{
    private readonly ICommitteePositionRecordRepository _repo = Substitute.For<ICommitteePositionRecordRepository>();
    private readonly ICommitteeTermRepository _termRepo = Substitute.For<ICommitteeTermRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public CommitteeServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _repo.AddAsync(Arg.Any<CommitteePositionRecord>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<CommitteePositionRecord>(0));
    }

    private CommitteeService CreateService() => new(_repo, _termRepo, _audit, _unitOfWork);

    // --- AddOrUpdateAsync ---

    [Fact]
    public async Task AddOrUpdateAsync_CreatesNewRecord_WhenNoneExistsForYear()
    {
        var memberId = Guid.NewGuid();
        _repo.GetByMemberAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(new List<CommitteePositionRecord>());

        var svc = CreateService();
        var result = await svc.AddOrUpdateAsync(memberId, 2026, "President", Ct);

        Assert.Equal(memberId, result.MemberId);
        Assert.Equal(2026, result.Year);
        Assert.Equal("President", result.Position);
        await _repo.Received(1).AddAsync(Arg.Any<CommitteePositionRecord>(), Arg.Any<CancellationToken>());
        await _audit.Received(1).LogAsync(nameof(CommitteePositionRecord), Arg.Any<Guid>(), Enums.AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddOrUpdateAsync_TrimsPosition_OnCreate()
    {
        var memberId = Guid.NewGuid();
        _repo.GetByMemberAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(new List<CommitteePositionRecord>());

        var svc = CreateService();
        var result = await svc.AddOrUpdateAsync(memberId, 2026, "  Treasurer  ", Ct);

        Assert.Equal("Treasurer", result.Position);
    }

    [Fact]
    public async Task AddOrUpdateAsync_UpdatesExistingRecord_WhenOneExistsForYear()
    {
        var memberId = Guid.NewGuid();
        var existing = new CommitteePositionRecord
        {
            Id = Guid.NewGuid(), MemberId = memberId, Year = 2026, Position = "Secretary",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _repo.GetByMemberAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(new List<CommitteePositionRecord> { existing });

        var svc = CreateService();
        var result = await svc.AddOrUpdateAsync(memberId, 2026, "President", Ct);

        Assert.Equal(existing.Id, result.Id);
        Assert.Equal("President", result.Position);
        await _repo.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().AddAsync(Arg.Any<CommitteePositionRecord>(), Arg.Any<CancellationToken>());
        await _audit.Received(1).LogAsync(nameof(CommitteePositionRecord), existing.Id, Enums.AuditAction.Update,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddOrUpdateAsync_OnlyMatchesRecordForRequestedYear()
    {
        var memberId = Guid.NewGuid();
        var lastYearRecord = new CommitteePositionRecord
        {
            Id = Guid.NewGuid(), MemberId = memberId, Year = 2025, Position = "Treasurer",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _repo.GetByMemberAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(new List<CommitteePositionRecord> { lastYearRecord });

        var svc = CreateService();
        await svc.AddOrUpdateAsync(memberId, 2026, "President", Ct);

        // A different year's record must not be touched; a new one is created instead.
        await _repo.Received(1).AddAsync(
            Arg.Is<CommitteePositionRecord>(r => r!.Year == 2026), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().UpdateAsync(lastYearRecord, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddOrUpdateAsync_WrapsWriteInTransaction()
    {
        var memberId = Guid.NewGuid();
        _repo.GetByMemberAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(new List<CommitteePositionRecord>());

        var svc = CreateService();
        await svc.AddOrUpdateAsync(memberId, 2026, "President", Ct);

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    // --- GetHistoryAsync ---

    [Fact]
    public async Task GetHistoryAsync_DelegatesToRepository()
    {
        var memberId = Guid.NewGuid();
        var history = new List<CommitteePositionRecord>
        {
            new() { Id = Guid.NewGuid(), MemberId = memberId, Year = 2025, Position = "Secretary",
                     CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };
        _repo.GetByMemberAsync(memberId, Arg.Any<CancellationToken>()).Returns(history);

        var svc = CreateService();
        var result = await svc.GetHistoryAsync(memberId, Ct);

        Assert.Same(history, result);
    }

    // --- GetCurrentAsync ---

    [Fact]
    public async Task GetCurrentAsync_ReturnsRecordsForOpenTerm_WhenOneExists()
    {
        var openTerm = new CommitteeTerm
        {
            Id = Guid.NewGuid(), StartedByAgmId = Guid.NewGuid(), StartDate = DateTime.UtcNow.Date,
            EndDate = null, LabelYear = 2026, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var records = new List<CommitteePositionRecord>
        {
            new() { Id = Guid.NewGuid(), MemberId = Guid.NewGuid(), CommitteeTermId = openTerm.Id,
                     CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };
        _termRepo.GetOpenAsync(Arg.Any<CancellationToken>()).Returns(openTerm);
        _repo.GetByTermAsync(openTerm.Id, Arg.Any<CancellationToken>()).Returns(records);

        var svc = CreateService();
        var result = await svc.GetCurrentAsync(Ct);

        Assert.Same(records, result);
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsEmpty_WhenNoOpenTermExists()
    {
        _termRepo.GetOpenAsync(Arg.Any<CancellationToken>()).Returns((CommitteeTerm?)null);

        var svc = CreateService();
        var result = await svc.GetCurrentAsync(Ct);

        Assert.Empty(result);
        await _repo.DidNotReceive().GetByTermAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // --- GetByTermAsync ---

    [Fact]
    public async Task GetByTermAsync_DelegatesToRepository()
    {
        var termId = Guid.NewGuid();
        var records = new List<CommitteePositionRecord>();
        _repo.GetByTermAsync(termId, Arg.Any<CancellationToken>()).Returns(records);

        var svc = CreateService();
        var result = await svc.GetByTermAsync(termId, Ct);

        Assert.Same(records, result);
    }

    // --- GetByAgmAsync ---

    [Fact]
    public async Task GetByAgmAsync_DelegatesToRepository()
    {
        var agmId = Guid.NewGuid();
        var records = new List<CommitteePositionRecord>();
        _repo.GetByAgmAsync(agmId, Arg.Any<CancellationToken>()).Returns(records);

        var svc = CreateService();
        var result = await svc.GetByAgmAsync(agmId, Ct);

        Assert.Same(records, result);
    }
}
