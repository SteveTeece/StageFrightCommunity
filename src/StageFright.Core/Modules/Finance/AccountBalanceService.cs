using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Computes GL-derived current balances for Chart of Accounts accounts, delegating to
/// IGLRepository.GetAccountBalanceAsync — the same inception-to-date calculation used by
/// BalanceSheetReportProvider. A failure computing one account's balance is isolated to
/// that account's row (HasError=true); every other account is unaffected.
/// </summary>
public class AccountBalanceService : IAccountBalanceService
{
    private static readonly AccountType[] CreditNormalTypes =
        [AccountType.Liability, AccountType.Equity, AccountType.Income];

    private readonly IAccountRepository _accountRepo;
    private readonly IGLRepository _glRepo;
    private readonly ILogger<AccountBalanceService> _logger;

    public AccountBalanceService(
        IAccountRepository accountRepo,
        IGLRepository glRepo,
        ILogger<AccountBalanceService> logger)
    {
        _accountRepo = accountRepo;
        _glRepo = glRepo;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AccountBalance>> GetActiveAccountBalancesAsync(CancellationToken ct = default)
    {
        var accounts = await _accountRepo.GetAllAsync(ct);
        return await BuildBalancesAsync(accounts, ct);
    }

    public async Task<IReadOnlyList<AccountBalance>> GetArchivedAccountBalancesAsync(CancellationToken ct = default)
    {
        var accounts = await _accountRepo.GetArchivedAsync(ct);
        return await BuildBalancesAsync(accounts, ct);
    }

    private async Task<IReadOnlyList<AccountBalance>> BuildBalancesAsync(
        IReadOnlyList<Account> accounts, CancellationToken ct)
    {
        var asAt = DateTime.UtcNow;
        var results = new List<AccountBalance>();

        foreach (var account in accounts.OrderBy(a => a.AccountNumber))
        {
            var row = new AccountBalance
            {
                AccountId = account.Id,
                AccountNumber = account.AccountNumber,
                Name = account.Name,
                Type = account.Type,
                IsSystem = account.IsSystem,
                IsBankAccount = account.IsBankAccount
            };

            try
            {
                var netDebit = await _glRepo.GetAccountBalanceAsync(account.Id, asAt, ct);
                row.Balance = CreditNormalTypes.Contains(account.Type) ? -netDebit : netDebit;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to compute balance for account {AccountId} ({AccountNumber} {AccountName}); showing error indicator",
                    account.Id, account.AccountNumber, account.Name);
                row.HasError = true;
            }

            results.Add(row);
        }

        return results;
    }
}
