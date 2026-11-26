using StageFright.Core.Entities;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Everything the Banking page shows at the top (spec 033). Never persisted.
/// </summary>
public class BankingOverview
{
    /// <summary>Received items still awaiting deposit, oldest first.</summary>
    public IReadOnlyList<AwaitingDepositItem> AwaitingItems { get; init; } = [];

    /// <summary>Sum of <see cref="AwaitingItems"/>; equals the Undeposited Funds GL balance.</summary>
    public decimal UndepositedTotal { get; init; }

    /// <summary>Current Petty Cash GL balance.</summary>
    public decimal PettyCashBalance { get; init; }

    /// <summary>Cash available to deposit today (research §8).</summary>
    public decimal CashAvailableToday { get; init; }

    /// <summary>Bank accounts a deposit or withdrawal can use: bank accounts other than Petty Cash. Empty means none is set up.</summary>
    public IReadOnlyList<Account> DepositBankAccounts { get; init; } = [];
}
