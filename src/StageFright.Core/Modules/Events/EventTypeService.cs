using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Modules.Events;

/// <summary>
/// Application service for event type management.
/// System defaults (IsSystemDefault=true) cannot be archived or renamed.
/// Archive is blocked while any non-deleted Event references the type.
/// </summary>
public class EventTypeService : IEventTypeService
{
    private readonly IEventTypeRepository _repo;
    private readonly IAuditTrailService _audit;
    private readonly ILocalizer _localizer;

    public EventTypeService(IEventTypeRepository repo, IAuditTrailService audit, ILocalizer localizer)
    {
        _repo = repo;
        _audit = audit;
        _localizer = localizer;
    }

    public Task<IReadOnlyList<EventType>> GetAllAsync(CancellationToken ct = default) =>
        _repo.GetAllAsync(ct);

    public async Task<IReadOnlyList<EventType>> GetSelectableForNewEventsAsync(CancellationToken ct = default)
    {
        var all = await _repo.GetAllAsync(ct);
        return all.Where(et => !string.Equals(et.Name, "Annual General Meeting", StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public Task<IReadOnlyList<EventType>> GetArchivedAsync(CancellationToken ct = default) =>
        _repo.GetArchivedAsync(ct);

    public async Task<EventType> CreateAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_EventType_NameRequired"), nameof(EventType), nameof(CreateAsync));

        var now = DateTime.UtcNow;
        var eventType = new EventType
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            IsSystemDefault = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var saved = await _repo.AddAsync(eventType, ct);
        await _audit.LogAsync(nameof(EventType), saved.Id, AuditAction.Create,
            oldValue: null, newValue: name, ct: ct);
        return saved;
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var eventType = await _repo.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(EventType), id, nameof(ArchiveAsync));

        if (eventType.IsSystemDefault)
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_EventType_SystemDefaultCannotArchive"),
                nameof(EventType), nameof(ArchiveAsync), id);

        if (await _repo.IsReferencedByEventsAsync(id, ct))
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_EventType_ReferencedCannotArchive"),
                nameof(EventType), nameof(ArchiveAsync), id);

        await _repo.ArchiveAsync(id, "system", ct);
        await _audit.LogAsync(nameof(EventType), id, AuditAction.Delete,
            oldValue: eventType.Name, newValue: null, ct: ct);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        await _repo.RestoreAsync(id, ct);
        await _audit.LogAsync(nameof(EventType), id, AuditAction.Restore,
            oldValue: null, newValue: null, ct: ct);
    }

    /// <summary>
    /// Returns the canonical list of system default event type names (seeded at first-run setup).
    /// </summary>
    public static IReadOnlyList<string> GetDefaultEventTypeNames() =>
    [
        "Performance",
        "Eisteddfod",
        "Fund raiser",
        "Promotional"
    ];
}
