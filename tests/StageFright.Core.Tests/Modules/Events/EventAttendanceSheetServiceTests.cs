using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Events;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Events;

/// <summary>
/// Unit tests for EventAttendanceSheetService — event lookup, point-in-time active-membership
/// filtering, sorting, and real/blank Participated computation.
/// </summary>
public class EventAttendanceSheetServiceTests : TestBase
{
    private readonly IEventRepository _eventRepo = Substitute.For<IEventRepository>();
    private readonly IMemberRepository _memberRepo = Substitute.For<IMemberRepository>();

    private EventAttendanceSheetService CreateService() => new(_eventRepo, _memberRepo);

    private static readonly Guid EventTypeId = Guid.NewGuid();

    private static EventType APerformanceType() => new()
    {
        Id = EventTypeId,
        Name = "Performance",
        IsSystemDefault = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Event AnEvent(Guid id, EventType? eventType = null, IEnumerable<ParticipationRecord>? participationRecords = null) => new()
    {
        Id = id,
        Date = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
        EventTypeId = EventTypeId,
        EventType = eventType ?? APerformanceType(),
        ParticipationRecords = (participationRecords ?? Enumerable.Empty<ParticipationRecord>()).ToList(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Member AMember(string firstName, string lastName) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = firstName,
        LastName = lastName,
        StreetAddress = "1 Test St",
        Status = MemberStatus.Active,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GenerateAsync_Throws_EntityNotFoundException_WhenEventUnknown()
    {
        var svc = CreateService();
        var eventId = Guid.NewGuid();
        _eventRepo.GetByIdWithDetailsAsync(eventId, Arg.Any<CancellationToken>()).Returns((Event?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => svc.GenerateAsync(eventId, Ct));
    }

    [Fact]
    public async Task GenerateAsync_Returns_OnlyMembersActiveAsOfEventDate()
    {
        var svc = CreateService();
        var evt = AnEvent(Guid.NewGuid());
        var member = AMember("Alice", "Anderson");
        _eventRepo.GetByIdWithDetailsAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(evt);
        _memberRepo.GetActiveAsOfAsync(evt.Date, Arg.Any<CancellationToken>()).Returns(new List<Member> { member });

        var result = await svc.GenerateAsync(evt.Id, Ct);

        Assert.Single(result.Members);
        Assert.Equal("Alice", result.Members[0].FirstName);
        Assert.Equal("Anderson", result.Members[0].LastName);
        await _memberRepo.Received(1).GetActiveAsOfAsync(evt.Date, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_Orders_BySurnameThenFirstName()
    {
        var svc = CreateService();
        var evt = AnEvent(Guid.NewGuid());
        var smithBob = AMember("Bob", "Smith");
        var smithAlice = AMember("Alice", "Smith");
        var jones = AMember("Zoe", "Jones");

        _eventRepo.GetByIdWithDetailsAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(evt);
        _memberRepo.GetActiveAsOfAsync(evt.Date, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { smithBob, jones, smithAlice });

        var result = await svc.GenerateAsync(evt.Id, Ct);

        Assert.Equal(3, result.Members.Count);
        Assert.Equal("Jones", result.Members[0].LastName);
        Assert.Equal("Smith", result.Members[1].LastName);
        Assert.Equal("Alice", result.Members[1].FirstName); // same-surname sub-sort by first name
        Assert.Equal("Smith", result.Members[2].LastName);
        Assert.Equal("Bob", result.Members[2].FirstName);
    }

    [Fact]
    public async Task GenerateAsync_Returns_EmptyMembersList_WhenNoActiveMembers()
    {
        var svc = CreateService();
        var evt = AnEvent(Guid.NewGuid());

        _eventRepo.GetByIdWithDetailsAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(evt);
        _memberRepo.GetActiveAsOfAsync(evt.Date, Arg.Any<CancellationToken>()).Returns(new List<Member>());

        var result = await svc.GenerateAsync(evt.Id, Ct);

        Assert.Empty(result.Members);
    }

    [Fact]
    public async Task GenerateAsync_Copies_EventDateAndEventTypeName()
    {
        var svc = CreateService();
        var evt = AnEvent(Guid.NewGuid());

        _eventRepo.GetByIdWithDetailsAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(evt);
        _memberRepo.GetActiveAsOfAsync(evt.Date, Arg.Any<CancellationToken>()).Returns(new List<Member>());

        var result = await svc.GenerateAsync(evt.Id, Ct);

        Assert.Equal(evt.Date, result.EventDate);
        Assert.Equal("Performance", result.EventTypeName);
    }

    // --- Participated computation ---

    [Fact]
    public async Task GenerateAsync_Participated_False_WhenNoParticipationRecordYet()
    {
        var svc = CreateService();
        var evt = AnEvent(Guid.NewGuid());
        var member = AMember("Alice", "Anderson");

        _eventRepo.GetByIdWithDetailsAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(evt);
        _memberRepo.GetActiveAsOfAsync(evt.Date, Arg.Any<CancellationToken>()).Returns(new List<Member> { member });

        var result = await svc.GenerateAsync(evt.Id, Ct);

        Assert.False(result.Members[0].Participated);
    }

    [Fact]
    public async Task GenerateAsync_Participated_True_WhenRecordMarksParticipated()
    {
        var svc = CreateService();
        var member = AMember("Bob", "Baker");
        var record = new ParticipationRecord { Id = Guid.NewGuid(), MemberId = member.Id, Participated = true, CreatedAt = DateTime.UtcNow };
        var evt = AnEvent(Guid.NewGuid(), participationRecords: new[] { record });
        record.EventId = evt.Id;

        _eventRepo.GetByIdWithDetailsAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(evt);
        _memberRepo.GetActiveAsOfAsync(evt.Date, Arg.Any<CancellationToken>()).Returns(new List<Member> { member });

        var result = await svc.GenerateAsync(evt.Id, Ct);

        Assert.True(result.Members[0].Participated);
    }

    [Fact]
    public async Task GenerateAsync_Participated_False_WhenRecordMarksNotParticipated()
    {
        var svc = CreateService();
        var member = AMember("Carol", "Clark");
        var record = new ParticipationRecord { Id = Guid.NewGuid(), MemberId = member.Id, Participated = false, CreatedAt = DateTime.UtcNow };
        var evt = AnEvent(Guid.NewGuid(), participationRecords: new[] { record });
        record.EventId = evt.Id;

        _eventRepo.GetByIdWithDetailsAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(evt);
        _memberRepo.GetActiveAsOfAsync(evt.Date, Arg.Any<CancellationToken>()).Returns(new List<Member> { member });

        var result = await svc.GenerateAsync(evt.Id, Ct);

        Assert.False(result.Members[0].Participated);
    }

    [Fact]
    public async Task GenerateAsync_Returns_ExactlyWhatMemberRepositoryReturns_ArchivedMembersExcluded()
    {
        // GetActiveAsOfAsync already excludes archived/soft-deleted members (IsDeleted=false at
        // its own boundary) — the service must not add any filtering of its own that could
        // re-include one; returning exactly what the repo returns proves this (FR-002/Scenario 3).
        var svc = CreateService();
        var evt = AnEvent(Guid.NewGuid());
        var activeMember = AMember("Dana", "Diaz");

        _eventRepo.GetByIdWithDetailsAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(evt);
        _memberRepo.GetActiveAsOfAsync(evt.Date, Arg.Any<CancellationToken>()).Returns(new List<Member> { activeMember });

        var result = await svc.GenerateAsync(evt.Id, Ct);

        Assert.Single(result.Members);
        Assert.Equal("Diaz", result.Members[0].LastName);
    }
}
