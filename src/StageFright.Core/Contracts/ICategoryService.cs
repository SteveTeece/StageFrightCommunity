using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for category CRUD, GL account assignment, archival, and reordering.
/// System categories (IsSystem=true) are immutable — edit and archive are blocked.
/// Archive is also blocked when the category is referenced by existing transactions.
/// </summary>
public interface ICategoryService
{
    /// <summary>Returns all active (non-archived) categories.</summary>
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Returns all archived categories.</summary>
    Task<IReadOnlyList<Category>> GetArchivedAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a new user-defined category and assigns the next sequential GL account number.
    /// Income → "1000", "1001", … | Expense → "2000", "2001", …
    /// </summary>
    Task<Category> CreateAsync(string name, CategoryType type, CancellationToken ct = default);

    /// <summary>
    /// Archives the category.
    /// Throws <see cref="Core.Exceptions.ValidationException"/> if IsSystem=true or referenced by transactions.
    /// </summary>
    Task ArchiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>Restores a previously archived category.</summary>
    Task RestoreAsync(Guid id, CancellationToken ct = default);

    /// <summary>Applies the provided sort-order values to the matching category records.</summary>
    Task ReorderAsync(IReadOnlyList<(Guid Id, int SortOrder)> order, CancellationToken ct = default);
}
