using StageFright.Core.Entities;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// One account's posted opening balance, signed the way it was entered: positive is the
/// account's normal side (debit for Asset/Expense, credit for Liability/Equity/Income).
/// </summary>
public class RecordedOpeningBalanceLine
{
    /// <summary>The account the balance was posted to.</summary>
    public Account Account { get; set; } = null!;

    /// <summary>The posted balance, positive on the account's normal side.</summary>
    public decimal Amount { get; set; }
}
