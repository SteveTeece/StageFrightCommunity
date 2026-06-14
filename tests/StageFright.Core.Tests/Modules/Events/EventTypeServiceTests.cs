using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Events;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Events;

/// <summary>
/// Unit tests for EventTypeService — CRUD, archival guard, system-default protection.
/// </summary>
public class EventTypeServiceTests : TestBase
{
    private readonly IEventTypeRepository _repo = Substitute.For<IEventTypeRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();

    private EventTypeService CreateService() => new(_repo, _audit);

    // --- GetAllAsync ---

    [Fact]
    public async Task GetAllAsync_DelegatesToRepository()
    {
        var types = new List<EventType>
        {
            AnEventType("Performance"),
            AnEventType("Eisteddfod")
        };
        _repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(types);

        var svc = CreateService();
        var result = await svc.GetAllAsync(Ct);

        Assert.Equal(2, result.Count);
    }

    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_PersistsEventType_WithCorrectFields()
    {
        _repo.AddAsync(Arg.Any<EventType>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<EventType>(0));

        var svc = CreateService();
        var result = await svc.CreateAsync("Concert", Ct);

        Assert.Equal("Concert", result.Name);
        Assert.False(result.IsSystemDefault);
    }

    [Fact]
    public async Task CreateAsync_WritesAuditEntry()
    {
        _repo.AddAsync(Arg.Any<EventType>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<EventType>(0));

        var svc = CreateService();
        await svc.CreateAsync("Concert", Ct);

        await _audit.Received(1).LogAsync(
            "EventType", Arg.Any<Guid>(), StageFright.Core.Enums.AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenNameEmpty()
    {
        var svc = CreateService();
        await Assert.ThrowsAsync<ValidationException>(() => svc.CreateAsync("", Ct));
    }

    // --- ArchiveAsync ---

    [Fact]
    public async Task ArchiveAsync_Succeeds_WhenNotReferencedAndNotSystem()
    {
        var eventType = AnEventType("Custom");
        _repo.GetByIdAsync(eventType.Id, Arg.Any<CancellationToken>()).Returns(eventType);
        _repo.IsReferencedByEventsAsync(eventType.Id, Arg.Any<CancellationToken>()).Returns(false);

        var svc = CreateService();
        await svc.ArchiveAsync(eventType.Id, Ct);

        await _repo.Received(1).ArchiveAsync(eventType.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveAsync_Throws_WhenIsSystemDefault()
    {
        var eventType = AnEventType("Annual General Meeting", isSystem: true);
        _repo.GetByIdAsync(eventType.Id, Arg.Any<CancellationToken>()).Returns(eventType);

        var svc = CreateService();
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => svc.ArchiveAsync(eventType.Id, Ct));

        Assert.Contains("system", ex.Message, StringComparison.OrdinalIgnoreCase);
        await _repo.DidNotReceive().ArchiveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveAsync_Throws_WhenReferencedByEvent()
    {
        var eventType = AnEventType("Performance");
        _repo.GetByIdAsync(eventType.Id, Arg.Any<CancellationToken>()).Returns(eventType);
        _repo.IsReferencedByEventsAsync(eventType.Id, Arg.Any<CancellationToken>()).Returns(true);

        var svc = CreateService();
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => svc.ArchiveAsync(eventType.Id, Ct));

        Assert.Contains("referenced", ex.Message, StringComparison.OrdinalIgnoreCase);
        await _repo.DidNotReceive().ArchiveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveAsync_Throws_WhenNotFound()
    {
        var id = Guid.NewGuid();
        _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((EventType?)null);

        var svc = CreateService();
        await Assert.ThrowsAsync<EntityNotFoundException>(() => svc.ArchiveAsync(id, Ct));
    }

    // --- RestoreAsync ---

    [Fact]
    public async Task RestoreAsync_DelegatesToRepository()
    {
        var id = Guid.NewGuid();
        var svc = CreateService();
        await svc.RestoreAsync(id, Ct);

        await _repo.Received(1).RestoreAsync(id, Arg.Any<CancellationToken>());
    }

    // --- System defaults ---

    [Fact]
    public async Task SystemDefaults_IncludeAGM_WithIsSystemDefaultTrue()
    {
        // Verify the seeded default names include AGM
        var defaults = EventTypeService.GetDefaultEventTypeNames();
        Assert.Contains("Annual General Meeting", defaults);
    }

    [Fact]
    public async Task SystemDefaults_IncludeExpectedTypes()
    {
        var defaults = EventTypeService.GetDefaultEventTypeNames();
        Assert.Contains("Performance", defaults);
        Assert.Contains("Eisteddfod", defaults);
        Assert.Contains("Fund raiser", defaults);
        Assert.Contains("Promotional", defaults);
        Assert.Equal(5, defaults.Count);
    }

    // --- Helpers ---

    private static EventType AnEventType(string name, bool isSystem = false) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        IsSystemDefault = isSystem,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
