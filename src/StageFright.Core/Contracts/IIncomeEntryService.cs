using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for recording non-member income such as raffles,
/// fundraising events, donations, and other miscellaneous income sources.
/// </summary>
public interface IIncomeEntryService
{
    /// <summary>
    /// Records an income entry with a matching GL pair: Debit Cash (0100) / Credit the selected Income category.
    /// The category must be of type Income and must not be a system category.
    /// </summary>
    Task RecordIncomeAsync(RecordIncomeRequest request, CancellationToken ct = default);

    /// <summary>Returns all active (non-archived) Income categories excluding system categories.</summary>
    Task<IReadOnlyList<Core.Entities.Category>> GetIncomeCategoriesAsync(CancellationToken ct = default);
}
