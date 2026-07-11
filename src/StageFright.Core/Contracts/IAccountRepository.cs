using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.Core.Contracts;

/// <summary>Repository contract for chart-of-accounts entities.</summary>
public interface IAccountRepository : ISoftDeletableRepository<Account>
{
    /// <summary>Returns true if any Transaction (including all historical) references the given account.</summary>
    Task<bool> IsReferencedByTransactionsAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// Returns the next available account number for the given type as max-in-range + 1
    /// over non-system accounts, including archived accounts (they still own their numbers).
    /// Range starts: Asset (bank) 1110, Asset (non-bank) 1300, Liability 2000, Equity 3300,
    /// Income 4000, Expense 6000.
    /// </summary>
    Task<string> GetNextAccountNumberAsync(AccountType type, bool isBank, CancellationToken ct = default);

    /// <summary>Applies the provided sort-order values to the matching account records.</summary>
    Task ReorderAsync(IReadOnlyList<(Guid Id, int SortOrder)> order, CancellationToken ct = default);
}
