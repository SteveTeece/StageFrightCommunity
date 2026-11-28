using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Default <see cref="ISystemAccountNameService"/> (issue #394). Each system account's name comes
/// from <see cref="SystemAccountsResource"/>. It is written to the account itself so that every
/// screen, report, PDF and CSV export shows it in the language in use, with no change at any
/// display site. System accounts cannot be edited by the user, so nothing typed by the user is
/// ever overwritten.
/// </summary>
/// <remarks>
/// A system account keeps its current name when an organisation's own account already uses the
/// translated name, so account names stay unique. The rename is not written to the audit trail:
/// it changes how a name reads, not any financial record.
/// </remarks>
public sealed class SystemAccountNameService : ISystemAccountNameService
{
    /// <summary>The <see cref="SystemAccountsResource"/> key for each system account's name.</summary>
    public static IReadOnlyDictionary<Guid, string> NameKeys { get; } = new Dictionary<Guid, string>
    {
        [SystemAccounts.CashId] = "SystemAccount_Name_PettyCash",
        [SystemAccounts.UndepositedFundsId] = "SystemAccount_Name_UndepositedFunds",
        [SystemAccounts.MemberReceivableId] = "SystemAccount_Name_MemberReceivable",
        [SystemAccounts.TaxCollectedId] = "SystemAccount_Name_TaxCollected",
        [SystemAccounts.TaxPaidId] = "SystemAccount_Name_TaxReceivable",
        [SystemAccounts.OpeningBalanceEquityId] = "SystemAccount_Name_OpeningBalanceEquity",
        [SystemAccounts.AccumulatedSurplusId] = "SystemAccount_Name_AccumulatedSurplus",
        [SystemAccounts.BadDebtId] = "SystemAccount_Name_BadDebtExpense",
    };

    private readonly IAccountRepository _repo;
    private readonly ILocalizer _localizer;
    private readonly ILogger<SystemAccountNameService> _logger;

    public SystemAccountNameService(
        IAccountRepository repo,
        ILocalizer localizer,
        ILogger<SystemAccountNameService> logger)
    {
        _repo = repo;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task ApplyAsync(CancellationToken ct = default)
    {
        try
        {
            var accounts = await _repo.GetAllAsync(ct);

            foreach (var account in accounts.Where(a => a.IsSystem))
            {
                if (!NameKeys.TryGetValue(account.Id, out var key))
                    continue;

                var name = _localizer.Get<SystemAccountsResource>(key);
                if (string.Equals(account.Name, name, StringComparison.Ordinal))
                    continue;

                var nameTaken = accounts.Any(a =>
                    a.Id != account.Id && string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
                if (nameTaken)
                {
                    _logger.LogWarning(
                        "System account {AccountNumber} keeps the name '{CurrentName}': another account is already named '{Name}'",
                        account.AccountNumber, account.Name, name);
                    continue;
                }

                account.Name = name;
                account.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(account, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to apply the system account names for the current language");
        }
    }
}
