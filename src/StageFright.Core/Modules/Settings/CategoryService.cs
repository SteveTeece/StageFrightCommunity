using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Modules.Settings;

/// <summary>
/// Application service for category management.
/// Assigns GL accounts sequentially on creation, enforces system-category immutability,
/// blocks archival of referenced categories, and writes audit trail entries.
/// </summary>
public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _repo;
    private readonly GLAccountAssignmentService _glAssignment;
    private readonly IAuditTrailService _audit;

    public CategoryService(
        ICategoryRepository repo,
        GLAccountAssignmentService glAssignment,
        IAuditTrailService audit)
    {
        _repo = repo;
        _glAssignment = glAssignment;
        _audit = audit;
    }

    public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default) =>
        _repo.GetAllAsync(ct);

    public Task<IReadOnlyList<Category>> GetArchivedAsync(CancellationToken ct = default) =>
        _repo.GetArchivedAsync(ct);

    public async Task<Category> CreateAsync(string name, CategoryType type, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Category name is required.", nameof(Category), nameof(CreateAsync));

        var glAccount = await _glAssignment.AssignNextAsync(type, ct);
        var now = DateTime.UtcNow;

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Type = type,
            GLAccount = glAccount,
            SortOrder = 0,
            IsSystem = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var saved = await _repo.AddAsync(category, ct);
        await _audit.LogAsync(nameof(Category), saved.Id, AuditAction.Create,
            oldValue: null, newValue: $"{name} (GL: {glAccount})", ct: ct);
        return saved;
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _repo.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id, nameof(ArchiveAsync));

        if (category.IsSystem)
            throw new ValidationException(
                "System categories cannot be archived.",
                nameof(Category), nameof(ArchiveAsync), id);

        if (await _repo.IsReferencedByTransactionsAsync(id, ct))
            throw new ValidationException(
                "This category cannot be archived because it is referenced by one or more transactions.",
                nameof(Category), nameof(ArchiveAsync), id);

        await _repo.ArchiveAsync(id, "system", ct);
        await _audit.LogAsync(nameof(Category), id, AuditAction.Delete,
            oldValue: category.Name, newValue: null, ct: ct);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        await _repo.RestoreAsync(id, ct);
        await _audit.LogAsync(nameof(Category), id, AuditAction.Restore,
            oldValue: null, newValue: null, ct: ct);
    }

    public Task ReorderAsync(IReadOnlyList<(Guid Id, int SortOrder)> order, CancellationToken ct = default) =>
        _repo.ReorderAsync(order, ct);
}
