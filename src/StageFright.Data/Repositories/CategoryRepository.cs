using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Data.Repositories;

public class CategoryRepository : SoftDeletableBaseRepository<Category>, ICategoryRepository
{
    public CategoryRepository(StageFrightDbContext db) : base(db) { }

    public async Task<bool> IsReferencedByTransactionsAsync(Guid categoryId, CancellationToken ct = default)
    {
        return await _db.Transactions.AnyAsync(t => t.CategoryId == categoryId, ct);
    }

    public async Task<string> GetNextGLAccountAsync(CategoryType type, CancellationToken ct = default)
    {
        var count = await _db.Categories
            .IgnoreQueryFilters()
            .Where(c => c.Type == type && !c.IsSystem)
            .CountAsync(ct);

        return type == CategoryType.Income
            ? $"{1000 + count:D4}"
            : $"{2000 + count:D4}";
    }

    public async Task ReorderAsync(IReadOnlyList<(Guid Id, int SortOrder)> order, CancellationToken ct = default)
    {
        foreach (var (id, sortOrder) in order)
        {
            var category = await _db.Categories.FindAsync(new object[] { id }, ct);
            if (category is not null)
            {
                category.SortOrder = sortOrder;
                category.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    public override async Task ArchiveAsync(Guid id, string deletedBy, CancellationToken ct = default)
    {
        var category = await _db.Categories.FindAsync(new object[] { id }, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id, nameof(ArchiveAsync));

        if (category.IsSystem)
            throw new ValidationException("System categories cannot be archived.", nameof(Category), nameof(ArchiveAsync), id);

        if (await IsReferencedByTransactionsAsync(id, ct))
            throw new ValidationException(
                "This category cannot be archived because it is referenced by one or more transactions.",
                nameof(Category), nameof(ArchiveAsync), id);

        await base.ArchiveAsync(id, deletedBy, ct);
    }
}
