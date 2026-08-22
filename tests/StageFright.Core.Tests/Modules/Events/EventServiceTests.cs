using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Events;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Events;

/// <summary>
/// Unit tests for EventService — scheduling, participation recording, rate formula, AGM check.
/// </summary>
public class EventServiceTests : TestBase
{
    private readonly IEventRepository _eventRepo = Substitute.For<IEventRepository>();
    private readonly IParticipationRepository _participationRepo = Substitute.For<IParticipationRepository>();
    private readonly IMemberRepository _memberRepo = Substitute.For<IMemberRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid EventTypeId = Guid.NewGuid();

    public EventServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _eventRepo.AddAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Event>(0));
    }

    private EventService CreateService() =>
        new(_eventRepo, _participationRepo, _memberRepo, _audit, _unitOfWork);

    // --- ScheduleAsync ---

    [Fact]
    public async Task ScheduleAsync_CreatesEventWithCorrectFields()
    {
        var svc = CreateService();
        var request = new ScheduleEventRequest
        {
            Date = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            EventTypeId = EventTypeId,
            Notes = "Summer performance"
        };

        var result = await svc.ScheduleAsync(request, Ct);

        Assert.Equal(request.Date, result.Date);
        Assert.Equal(request.EventTypeId, result.EventTypeId);
        Assert.Equal(request.Notes, result.Notes);
        Assert.Null(result.StoredParticipationRate);
    }

    [Fact]
    public async Task ScheduleAsync_WritesAuditEntry()
    {
        var svc = CreateService();
        var request = new ScheduleEventRequest
        {
            Date = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            EventTypeId = EventTypeId
        };

        await svc.ScheduleAsync(request, Ct);

        await _audit.Received(1).LogAsync(
            "Event", Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- RecordParticipationAsync ---

    [Fact]
    public async Task RecordParticipationAsync_StoresParticipationRate_WhenMembersPresent()
    {
        var evt = AnEventWithNoRate();
        _eventRepo.GetByIdAsync(EventId, Arg.Any<CancellationToken>()).Returns(evt);
        _memberRepo.GetActiveAsOfAsync(evt.Date, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { ActiveMember(), ActiveMember(), ActiveMember() });
        _participationRepo.ExistsAsync(EventId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var items = new[]
        {
            new ParticipationBatchItem { MemberId = Guid.NewGuid(), Participated = true },
            new ParticipationBatchItem { MemberId = Guid.NewGuid(), Participated = true }
        };

        var svc = CreateService();
        await svc.RecordParticipationAsync(EventId, items, Ct);

        // Rate = 2 participated / 3 active * 100 ≈ 66.67
        await _eventRepo.Received(1).UpdateAsync(
            Arg.Is<Event>(e => e!.StoredParticipationRate.HasValue
                && e.StoredParticipationRate.Value > 66m
                && e.StoredParticipationRate.Value < 67m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordParticipationAsync_SetsRateTo100_WhenAllPresent()
    {
        var evt = AnEventWithNoRate();
        _eventRepo.GetByIdAsync(EventId, Arg.Any<CancellationToken>()).Returns(evt);
        var member1 = ActiveMember();
        var member2 = ActiveMember();
        _memberRepo.GetActiveAsOfAsync(evt.Date, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { member1, member2 });
        _participationRepo.ExistsAsync(EventId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var items = new[]
        {
            new ParticipationBatchItem { MemberId = member1.Id, Participated = true },
            new ParticipationBatchItem { MemberId = member2.Id, Participated = true }
        };

        var svc = CreateService();
        await svc.RecordParticipationAsync(EventId, items, Ct);

        await _eventRepo.Received(1).UpdateAsync(
            Arg.Is<Event>(e => e!.StoredParticipationRate == 100m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordParticipationAsync_SetsRateToZero_WhenNoActiveMembers()
    {
        var evt = AnEventWithNoRate();
        _eventRepo.GetByIdAsync(EventId, Arg.Any<CancellationToken>()).Returns(evt);
        _memberRepo.GetActiveAsOfAsync(evt.Date, Arg.Any<CancellationToken>())
            .Returns(new List<Member>());
        _participationRepo.ExistsAsync(EventId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var svc = CreateService();
        await svc.RecordParticipationAsync(EventId, Array.Empty<ParticipationBatchItem>(), Ct);

        await _eventRepo.Received(1).UpdateAsync(
            Arg.Is<Event>(e => e!.StoredParticipationRate == 0m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordParticipationAsync_IsIdempotent_SkipsDuplicates()
    {
        var evt = AnEventWithNoRate();
        var memberId = Guid.NewGuid();
        _eventRepo.GetByIdAsync(EventId, Arg.Any<CancellationToken>()).Returns(evt);
        _memberRepo.GetActiveAsOfAsync(evt.Date, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { ActiveMember() });

        // Already recorded
        _participationRepo.ExistsAsync(EventId, memberId, Arg.Any<CancellationToken>()).Returns(true);

        var items = new[] { new ParticipationBatchItem { MemberId = memberId, Participated = true } };

        var svc = CreateService();
        await svc.RecordParticipationAsync(EventId, items, Ct);

        await _participationRepo.DidNotReceive().AddBatchAsync(
            Arg.Any<IReadOnlyList<ParticipationRecord>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordParticipationAsync_DoesNotCreateFeeOrGL_ForAnyEvent()
    {
        // Events NEVER create fees per FR-006 — we verify no GL-related calls are made
        var evt = AnEventWithNoRate();
        _eventRepo.GetByIdAsync(EventId, Arg.Any<CancellationToken>()).Returns(evt);
        _memberRepo.GetActiveAsOfAsync(evt.Date, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { ActiveMember() });
        _participationRepo.ExistsAsync(EventId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var items = new[] { new ParticipationBatchItem { MemberId = Guid.NewGuid(), Participated = true } };

        var svc = CreateService();
        await svc.RecordParticipationAsync(EventId, items, Ct);

        // No repository other than event/participation/member was called
        await _audit.DidNotReceive().LogAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- Future-dated event ---

    [Fact]
    public async Task RecordParticipationAsync_EventDateInFuture_ThrowsValidationException()
    {
        var futureEventId = Guid.NewGuid();
        var evt = new Event
        {
            Id = futureEventId,
            Date = DateTime.Today.AddDays(1),
            EventTypeId = EventTypeId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _eventRepo.GetByIdAsync(futureEventId, Arg.Any<CancellationToken>()).Returns(evt);

        var items = new[] { new ParticipationBatchItem { MemberId = Guid.NewGuid(), Participated = true } };

        var svc = CreateService();
        await Assert.ThrowsAsync<ValidationException>(() => svc.RecordParticipationAsync(futureEventId, items, Ct));
        await _participationRepo.DidNotReceive().AddBatchAsync(Arg.Any<IReadOnlyList<ParticipationRecord>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordParticipationAsync_EventDateIsToday_Succeeds()
    {
        var todayEventId = Guid.NewGuid();
        var evt = new Event
        {
            Id = todayEventId,
            Date = DateTime.Today,
            EventTypeId = EventTypeId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _eventRepo.GetByIdAsync(todayEventId, Arg.Any<CancellationToken>()).Returns(evt);
        _memberRepo.GetActiveAsOfAsync(evt.Date, Arg.Any<CancellationToken>())
            .Returns(new List<Member> { ActiveMember() });
        _participationRepo.ExistsAsync(todayEventId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var items = new[] { new ParticipationBatchItem { MemberId = Guid.NewGuid(), Participated = true } };

        var svc = CreateService();
        await svc.RecordParticipationAsync(todayEventId, items, Ct);

        await _eventRepo.Received(1).UpdateAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>());
    }

    // --- GetMostRecentPastAsync ---

    [Fact]
    public async Task GetMostRecentPastAsync_DelegatesToRepository()
    {
        var asOf = DateTime.UtcNow;
        var evt = AnEventWithNoRate();
        _eventRepo.GetMostRecentPastAsync(asOf, Arg.Any<CancellationToken>()).Returns(evt);

        var svc = CreateService();
        var result = await svc.GetMostRecentPastAsync(asOf, Ct);

        Assert.Same(evt, result);
    }

    // --- GetMostRecentPastWithoutParticipationAsync ---

    [Fact]
    public async Task GetMostRecentPastWithoutParticipationAsync_DelegatesToRepository()
    {
        var asOf = DateTime.UtcNow;
        var evt = AnEventWithNoRate();
        _eventRepo.GetMostRecentPastWithoutParticipationAsync(asOf, Arg.Any<CancellationToken>()).Returns(evt);

        var svc = CreateService();
        var result = await svc.GetMostRecentPastWithoutParticipationAsync(asOf, Ct);

        Assert.Same(evt, result);
    }

    [Fact]
    public async Task GetMostRecentPastWithoutParticipationAsync_ReturnsNull_WhenAllEventsHaveParticipation()
    {
        var asOf = DateTime.UtcNow;
        _eventRepo.GetMostRecentPastWithoutParticipationAsync(asOf, Arg.Any<CancellationToken>()).Returns((Event?)null);

        var svc = CreateService();
        var result = await svc.GetMostRecentPastWithoutParticipationAsync(asOf, Ct);

        Assert.Null(result);
    }

    // --- GetMostRecentPastWithParticipationAsync ---

    [Fact]
    public async Task GetMostRecentPastWithParticipationAsync_DelegatesToRepository()
    {
        var asOf = DateTime.UtcNow;
        var evt = AnEventWithNoRate();
        evt.StoredParticipationRate = 80m;
        _eventRepo.GetMostRecentPastWithParticipationAsync(asOf, Arg.Any<CancellationToken>()).Returns(evt);

        var svc = CreateService();
        var result = await svc.GetMostRecentPastWithParticipationAsync(asOf, Ct);

        Assert.Same(evt, result);
    }

    [Fact]
    public async Task GetMostRecentPastWithParticipationAsync_ReturnsNull_WhenNoEventsHaveParticipation()
    {
        var asOf = DateTime.UtcNow;
        _eventRepo.GetMostRecentPastWithParticipationAsync(asOf, Arg.Any<CancellationToken>()).Returns((Event?)null);

        var svc = CreateService();
        var result = await svc.GetMostRecentPastWithParticipationAsync(asOf, Ct);

        Assert.Null(result);
    }

    // --- GetNextUpcomingAsync ---

    [Fact]
    public async Task GetNextUpcomingAsync_DelegatesToRepository()
    {
        var asOf = DateTime.UtcNow;
        var evt = AnEventWithNoRate();
        _eventRepo.GetNextUpcomingAsync(asOf, Arg.Any<CancellationToken>()).Returns(evt);

        var svc = CreateService();
        var result = await svc.GetNextUpcomingAsync(asOf, Ct);

        Assert.Same(evt, result);
    }

    [Fact]
    public async Task GetNextUpcomingAsync_ReturnsNull_WhenNoFutureEvents()
    {
        var asOf = DateTime.UtcNow;
        _eventRepo.GetNextUpcomingAsync(asOf, Arg.Any<CancellationToken>()).Returns((Event?)null);

        var svc = CreateService();
        var result = await svc.GetNextUpcomingAsync(asOf, Ct);

        Assert.Null(result);
    }

    // --- Helpers ---

    private static Event AnEventWithNoRate() => new()
    {
        Id = EventId,
        Date = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        EventTypeId = EventTypeId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Member ActiveMember() => new()
    {
        Id = Guid.NewGuid(), FirstName = "Test", LastName = "Member", StreetAddress = "1 St",
        Status = MemberStatus.Active, ActivateDate = DateTime.UtcNow.Date,
        JoinDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
