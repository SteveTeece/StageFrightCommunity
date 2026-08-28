using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Agm;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Events;
using StageFright.Core.Modules.Settings;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance test for V20: AGMs merged onto the All Events list (spec 023, issue #320). Seeds one
/// Event and one AnnualGeneralMeeting, confirms ICombinedEventListService.GetAllAsync returns both
/// with the correct per-kind DetailUrl routing (FR-006), and confirms archiving the AGM removes it
/// from the combined result while the Event row remains (FR-010). Uses a real SQLite in-memory
/// database with full EF migrations, matching V19's existing convention (no DI container).
/// </summary>
public sealed class V20_CombinedEventsListTests : IAsyncLifetime
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

    [Fact]
    public async Task GetAllAsync_ReturnsBothRows_WithCorrectRouting_AndArchivedAgmDisappears()
    {
        var eventType = await AddEventType("Performance");
        var eventSvc = BuildEventService();
        var agmSvc = BuildAgmService();
        var combinedSvc = new CombinedEventListService(eventSvc, agmSvc);

        var evt = await eventSvc.ScheduleAsync(new ScheduleEventRequest
        {
            Date = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EventTypeId = eventType.Id
        }, TestContext.Current.CancellationToken);

        var agm = await agmSvc.ScheduleAsync(
            new ScheduleAgmRequest(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), "Annual sitting"),
            TestContext.Current.CancellationToken);

        var beforeArchive = await combinedSvc.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, beforeArchive.Count);
        var eventRow = Assert.Single(beforeArchive, i => i.Id == evt.Id);
        var agmRow = Assert.Single(beforeArchive, i => i.Id == agm.Id);
        Assert.Equal($"/events/{evt.Id}", eventRow.DetailUrl);
        Assert.Equal($"/events/agm/{agm.Id}", agmRow.DetailUrl);
        Assert.NotEqual($"/events/{agm.Id}", agmRow.DetailUrl);

        // --- FR-010: a soft-deleted (archived) AGM disappears from the combined list ---
        await agmSvc.ArchiveAsync(agm.Id, "coordinator", TestContext.Current.CancellationToken);

        var afterArchive = await combinedSvc.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(afterArchive, i => i.Id == agm.Id);
        Assert.Contains(afterArchive, i => i.Id == evt.Id);
    }

    // --- Helpers ---

    private EventService BuildEventService()
    {
        var eventRepo = new EventRepository(_db);
        var participationRepo = new ParticipationRepository(_db);
        var memberRepo = new MemberRepository(_db);
        var auditSvc = BuildAuditService();
        var unitOfWork = new UnitOfWork(_db);
        return new EventService(eventRepo, participationRepo, memberRepo, auditSvc, unitOfWork, RealLocalizer.Instance);
    }

    private AgmService BuildAgmService()
    {
        var settingsRepo = new SettingsRepository(_db);
        var auditSvc = BuildAuditService();
        var settingsSvc = new SettingsService(settingsRepo, auditSvc, RealLocalizer.Instance);

        return new AgmService(
            new AgmRepository(_db),
            new AgmAttendanceRepository(_db),
            new CommitteeTermRepository(_db),
            new CommitteePositionRecordRepository(_db),
            settingsSvc,
            auditSvc,
            new UnitOfWork(_db),
            RealLocalizer.Instance);
    }

    private AuditTrailService BuildAuditService()
    {
        var auditRepo = new AuditTrailRepository(_db);
        return new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
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
}
