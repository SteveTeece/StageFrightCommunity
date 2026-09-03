using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Modules.Members;

/// <summary>
/// Manages committee office-holder titles: built-in President/Secretary/Treasurer (fixed at
/// DisplayOrder 0-2, never renamed/reordered/archived) plus coordinator-defined custom titles.
/// </summary>
public class CommitteeOfficeHolderTypeService : ICommitteeOfficeHolderTypeService
{
    private readonly ICommitteeOfficeHolderTypeRepository _repo;
    private readonly IAuditTrailService _audit;
    private readonly ILocalizer _localizer;

    public CommitteeOfficeHolderTypeService(ICommitteeOfficeHolderTypeRepository repo, IAuditTrailService audit, ILocalizer localizer)
    {
        _repo = repo;
        _audit = audit;
        _localizer = localizer;
    }

    public Task<IReadOnlyList<CommitteeOfficeHolderType>> GetActiveAsync(CancellationToken ct = default) =>
        _repo.GetActiveOrderedAsync(ct);

    public async Task<CommitteeOfficeHolderType> AddAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_OfficeHolderType_NameRequired"), nameof(CommitteeOfficeHolderType), nameof(AddAsync));

        var maxCustomOrder = await _repo.GetMaxCustomDisplayOrderAsync(ct);
        var nextOrder = Math.Max(maxCustomOrder ?? 2, 2) + 1;

        var now = DateTime.UtcNow;
        var entity = new CommitteeOfficeHolderType
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            DisplayOrder = nextOrder,
            IsBuiltIn = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var saved = await _repo.AddAsync(entity, ct);
        await _audit.LogAsync(nameof(CommitteeOfficeHolderType), saved.Id, AuditAction.Create, newValue: saved.Name, ct: ct);
        return saved;
    }

    public async Task RenameAsync(Guid id, string newName, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(CommitteeOfficeHolderType), id, nameof(RenameAsync));

        if (entity.IsBuiltIn)
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_OfficeHolderType_BuiltInCannotRename"), nameof(CommitteeOfficeHolderType), nameof(RenameAsync), id);

        if (string.IsNullOrWhiteSpace(newName))
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_OfficeHolderType_NameRequired"), nameof(CommitteeOfficeHolderType), nameof(RenameAsync), id);

        var oldName = entity.Name;
        entity.Name = newName.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(entity, ct);
        await _audit.LogAsync(nameof(CommitteeOfficeHolderType), id, AuditAction.Update, oldValue: oldName, newValue: entity.Name, ct: ct);
    }

    public async Task ReorderAsync(IReadOnlyList<Guid> orderedCustomTitleIds, CancellationToken ct = default)
    {
        var displayOrder = 3;
        foreach (var id in orderedCustomTitleIds)
        {
            var entity = await _repo.GetByIdAsync(id, ct)
                ?? throw new EntityNotFoundException(nameof(CommitteeOfficeHolderType), id, nameof(ReorderAsync));

            if (entity.IsBuiltIn)
                throw new ValidationException(_localizer.Get<ValidationResource>("Validation_OfficeHolderType_BuiltInCannotReorder"), nameof(CommitteeOfficeHolderType), nameof(ReorderAsync), id);

            entity.DisplayOrder = displayOrder++;
            entity.UpdatedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(entity, ct);
        }

        await _audit.LogAsync(nameof(CommitteeOfficeHolderType), Guid.Empty, AuditAction.Update, newValue: "Reordered custom titles", ct: ct);
    }

    public async Task ArchiveAsync(Guid id, string deletedBy, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(CommitteeOfficeHolderType), id, nameof(ArchiveAsync));

        if (entity.IsBuiltIn)
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_OfficeHolderType_BuiltInCannotArchive"), nameof(CommitteeOfficeHolderType), nameof(ArchiveAsync), id);

        await _repo.ArchiveAsync(id, deletedBy, ct);
        await _audit.LogAsync(nameof(CommitteeOfficeHolderType), id, AuditAction.Delete, oldValue: entity.Name, ct: ct);
    }
}
