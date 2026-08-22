using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Events;
using StageFright.Core.Modules.Members;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V5: event type management, event scheduling, and participation recording.
/// Verifies: event types created; AGM event → no fees; participation recorded → StoredParticipationRate frozen.
/// Uses a real SQLite in-memory database with full EF migrations.
/// </summary>
public sealed class V5_EventsParticipationTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- Event Type Management ---

    [Fact]
    public async Task CreateEventType_PersistsWithCorrectFields()
    {
        var svc = BuildEventTypeService();
        var created = await svc.CreateAsync("Charity Concert", TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Charity Concert", created.Name);
        Assert.False(created.IsSystemDefault);

        var found = await new EventTypeRepository(_db).GetByIdAsync(created.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(found);
        Assert.Equal("Charity Concert", found!.Name);
    }

    [Fact]
    public async Task ArchiveEventType_WhenNotReferenced_Succeeds()
    {
        var svc = BuildEventTypeService();
        var created = await svc.CreateAsync("Temporary Type", TestContext.Current.CancellationToken);

        await svc.ArchiveAsync(created.Id, TestContext.Current.CancellationToken);

        var active = await svc.GetAllAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(active, et => et.Id == created.Id);

        var archived = await svc.GetArchivedAsync(TestContext.Current.CancellationToken);
        Assert.Contains(archived, et => et.Id == created.Id);
    }

    [Fact]
    public async Task ArchiveEventType_WhenReferencedByEvent_Throws()
    {
        var svc = BuildEventTypeService();
        var eventType = await svc.CreateAsync("Referenced Type", TestContext.Current.CancellationToken);

        // Create event referencing this type
        var eventSvc = BuildEventService();
        await eventSvc.ScheduleAsync(new ScheduleEventRequest
        {
            Date = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            EventTypeId = eventType.Id
        }, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<StageFright.Core.Exceptions.ValidationException>(
            () => svc.ArchiveAsync(eventType.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ArchiveEventType_SystemDefault_Throws()
    {
        var systemType = await AddSystemEventType("Annual General Meeting");
        var svc = BuildEventTypeService();

        await Assert.ThrowsAsync<StageFright.Core.Exceptions.ValidationException>(
            () => svc.ArchiveAsync(systemType.Id, TestContext.Current.CancellationToken));
    }

    // --- Event Scheduling ---

    [Fact]
    public async Task ScheduleEvent_Persists_WithNullStoredParticipationRate()
    {
        var eventType = await AddEventType("Performance");
        var svc = BuildEventService();

        var evt = await svc.ScheduleAsync(new ScheduleEventRequest
        {
            Date = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc),
            EventTypeId = eventType.Id,
            Notes = "Annual concert"
        }, TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, evt.Id);
        Assert.Null(evt.StoredParticipationRate);

        var found = await new EventRepository(_db).GetByIdAsync(evt.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(found);
        Assert.Null(found!.StoredParticipationRate);
    }

    // --- AGM: No Fees Created ---

    [Fact]
    public async Task AGMEvent_RecordParticipation_CreatesNoFees_AndNoTransactions()
    {
        var agmType = await AddSystemEventType("Annual General Meeting");
        var member1 = await AddActiveMember("Carol");
        var member2 = await AddActiveMember("Dave");

        var eventSvc = BuildEventService();
        var agm = await eventSvc.ScheduleAsync(new ScheduleEventRequest
        {
            Date = DateTime.UtcNow.Date.AddDays(-1),
            EventTypeId = agmType.Id
        }, TestContext.Current.CancellationToken);

        await eventSvc.RecordParticipationAsync(agm.Id, new[]
        {
            new ParticipationBatchItem { MemberId = member1.Id, Participated = true },
            new ParticipationBatchItem { MemberId = member2.Id, Participated = true }
        }, TestContext.Current.CancellationToken);

        // Verify no fees or GL transactions created
        Assert.Equal(0, await _db.Fees.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, await _db.Transactions.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, await _db.Payments.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    // --- Participation Recording and Rate Freezing ---

    [Fact]
    public async Task RecordParticipation_FreezesStoredParticipationRate()
    {
        var eventType = await AddEventType("Performance");
        var member1 = await AddActiveMember("Alice");
        var member2 = await AddActiveMember("Bob");

        var eventSvc = BuildEventService();
        var evt = await eventSvc.ScheduleAsync(new ScheduleEventRequest
        {
            Date = DateTime.UtcNow.Date.AddDays(-1),
            EventTypeId = eventType.Id
        }, TestContext.Current.CancellationToken);

        await eventSvc.RecordParticipationAsync(evt.Id, new[]
        {
            new ParticipationBatchItem { MemberId = member1.Id, Participated = true },
            new ParticipationBatchItem { MemberId = member2.Id, Participated = false }
        }, TestContext.Current.CancellationToken);

        var updated = await new EventRepository(_db).GetByIdAsync(evt.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated!.StoredParticipationRate);
        // 1 participated out of 2 active = 50%
        Assert.Equal(50m, updated.StoredParticipationRate!.Value);
    }

    [Fact]
    public async Task RecordParticipation_IsIdempotent_NoDoubleRecording()
    {
        var eventType = await AddEventType("Performance");
        var member = await AddActiveMember("Eve");

        var eventSvc = BuildEventService();
        var evt = await eventSvc.ScheduleAsync(new ScheduleEventRequest
        {
            Date = DateTime.UtcNow.Date.AddDays(-1),
            EventTypeId = eventType.Id
        }, TestContext.Current.CancellationToken);

        var items = new[] { new ParticipationBatchItem { MemberId = member.Id, Participated = true } };
        await eventSvc.RecordParticipationAsync(evt.Id, items, TestContext.Current.CancellationToken);
        await eventSvc.RecordParticipationAsync(evt.Id, items, TestContext.Current.CancellationToken); // second call is idempotent

        // Only one participation record created
        Assert.Equal(1, await _db.ParticipationRecords.CountAsync(p => p.MemberId == member.Id, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RecordParticipation_RateIsFrozen_AfterFirstRecord()
    {
        var eventType = await AddEventType("Eisteddfod");
        var member = await AddActiveMember("Frank");

        var eventSvc = BuildEventService();
        var evt = await eventSvc.ScheduleAsync(new ScheduleEventRequest
        {
            Date = DateTime.UtcNow.Date.AddDays(-1),
            EventTypeId = eventType.Id
        }, TestContext.Current.CancellationToken);

        await eventSvc.RecordParticipationAsync(evt.Id, new[]
        {
            new ParticipationBatchItem { MemberId = member.Id, Participated = true }
        }, TestContext.Current.CancellationToken);

        var before = (await new EventRepository(_db).GetByIdAsync(evt.Id, TestContext.Current.CancellationToken))!.StoredParticipationRate;
        Assert.NotNull(before);

        // Calling again with an empty batch is a no-op — the rate must NOT change
        await eventSvc.RecordParticipationAsync(evt.Id, Array.Empty<ParticipationBatchItem>(), TestContext.Current.CancellationToken);

        var after = (await new EventRepository(_db).GetByIdAsync(evt.Id, TestContext.Current.CancellationToken))!.StoredParticipationRate;
        Assert.Equal(before, after);
    }

    // --- Helpers ---

    private EventService BuildEventService()
    {
        var eventRepo = new EventRepository(_db);
        var participationRepo = new ParticipationRepository(_db);
        var memberRepo = new MemberRepository(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditSvc = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var unitOfWork = new UnitOfWork(_db);
        return new EventService(eventRepo, participationRepo, memberRepo, auditSvc, unitOfWork);
    }

    private EventTypeService BuildEventTypeService()
    {
        var eventTypeRepo = new EventTypeRepository(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditSvc = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        return new EventTypeService(eventTypeRepo, auditSvc);
    }

    private async Task<EventType> AddEventType(string name)
    {
        var et = new EventType
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsSystemDefault = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.EventTypes.Add(et);
        await _db.SaveChangesAsync();
        return et;
    }

    private async Task<EventType> AddSystemEventType(string name)
    {
        var et = new EventType
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsSystemDefault = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.EventTypes.Add(et);
        await _db.SaveChangesAsync();
        return et;
    }

    private async Task<Member> AddActiveMember(string name)
    {
        var member = new Member
        {
            Id = Guid.NewGuid(), FirstName = name, StreetAddress = "1 Test St",
            Status = MemberStatus.Active,
            ActivateDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            JoinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync();
        return member;
    }
}
