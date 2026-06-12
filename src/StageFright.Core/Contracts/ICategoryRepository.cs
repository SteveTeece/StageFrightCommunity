using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.Core.Contracts;

/// <summary>Repository contract for Category entities.</summary>
public interface ICategoryRepository : ISoftDeletableRepository<Category>
{
    /// <summary>Returns true if any Transaction (including all historical) references the given category.</summary>
    Task<bool> IsReferencedByTransactionsAsync(Guid categoryId, CancellationToken ct = default);

    /// <summary>
    /// Returns the next sequential GL account string for the given type.
    /// Income: counts existing income categories by CreatedAt ASC → "1000", "1001", …
    /// Expense: "2000", "2001", …
    /// </summary>
    Task<string> GetNextGLAccountAsync(CategoryType type, CancellationToken ct = default);

    /// <summary>Applies the provided sort-order values to the matching category records.</summary>
    Task ReorderAsync(IReadOnlyList<(Guid Id, int SortOrder)> order, CancellationToken ct = default);
}
