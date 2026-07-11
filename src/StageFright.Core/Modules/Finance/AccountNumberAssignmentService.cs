using StageFright.Core.Contracts;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Assigns the next sequential account number for a new user-created account
/// (Australian small-NFP convention, max-in-range + 1 over non-system accounts,
/// archived accounts included):
/// Asset (bank) → 1110+, Asset (non-bank) → 1300+, Liability → 2000+,
/// Equity → 3300+, Income → 4000+, Expense → 6000+.
/// Fixed system accounts: Cash on Hand=1100, Member Receivable=1200, GST Collected=2310,
/// GST Paid=2320, Opening Balance Equity=3100, Accumulated Surplus=3200, Bad Debt=6999.
/// </summary>
public class AccountNumberAssignmentService
{
    private readonly IAccountRepository _accountRepository;

    public AccountNumberAssignmentService(IAccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
    }

    /// <summary>Returns the next available account number string for the given account type.</summary>
    public Task<string> AssignNextAsync(AccountType type, bool isBank = false, CancellationToken ct = default)
    {
        return _accountRepository.GetNextAccountNumberAsync(type, isBank, ct);
    }
}
