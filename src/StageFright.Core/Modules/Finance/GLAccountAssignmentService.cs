using StageFright.Core.Contracts;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Assigns the next sequential GL account number for a new user-created category.
/// Income → "1000", "1001", … (10xx range)
/// Expense → "2000", "2001", … (20xx range)
/// Fixed system accounts: Cash="0100", MemberReceivable="0101", BadDebtExpense="9900"
/// </summary>
public class GLAccountAssignmentService
{
    private readonly ICategoryRepository _categoryRepository;

    public GLAccountAssignmentService(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    /// <summary>Returns the next available GL account string for the given category type.</summary>
    public Task<string> AssignNextAsync(CategoryType type, CancellationToken ct = default)
    {
        return _categoryRepository.GetNextGLAccountAsync(type, ct);
    }
}
