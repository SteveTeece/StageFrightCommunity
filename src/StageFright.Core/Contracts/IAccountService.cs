using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.Core.Contracts;

/// <summary>
/// Application service for chart-of-accounts CRUD, account-number assignment, archival,
/// and reordering. System accounts (IsSystem=true) are immutable — edit and archive are
/// blocked. Archive is also blocked when the account is referenced by existing transactions.
/// </summary>
public interface IAccountService
{
    /// <summary>Returns all active (non-archived) accounts.</summary>
    Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Returns all archived accounts.</summary>
    Task<IReadOnlyList<Account>> GetArchivedAsync(CancellationToken ct = default);

    /// <summary>Returns all active bank/cash accounts (IsBankAccount), ordered by account number.</summary>
    Task<IReadOnlyList<Account>> GetBankAccountsAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a new user-defined account and assigns the next sequential account number
    /// for its type range. The bank flag is only permitted on Asset accounts.
    /// Throws <see cref="Core.Exceptions.ValidationException"/> on missing/duplicate name
    /// or an invalid bank flag.
    /// </summary>
    Task<Account> CreateAsync(string name, AccountType type, bool isBankAccount = false, CancellationToken ct = default);

    /// <summary>
    /// Renames a user-defined account. Type, number, and bank flag are immutable.
    /// Throws <see cref="Core.Exceptions.ValidationException"/> if the account is a system
    /// account or the name is missing/duplicate.
    /// </summary>
    Task UpdateAsync(Guid id, string name, CancellationToken ct = default);

    /// <summary>
    /// Archives the account.
    /// Throws <see cref="Core.Exceptions.ValidationException"/> if IsSystem=true or referenced by transactions.
    /// </summary>
    Task ArchiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>Restores a previously archived account.</summary>
    Task RestoreAsync(Guid id, CancellationToken ct = default);

    /// <summary>Applies the provided sort-order values to the matching account records.</summary>
    Task ReorderAsync(IReadOnlyList<(Guid Id, int SortOrder)> order, CancellationToken ct = default);
}
