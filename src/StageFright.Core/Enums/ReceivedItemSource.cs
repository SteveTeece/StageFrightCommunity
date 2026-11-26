namespace StageFright.Core.Enums;

/// <summary>
/// Where a received item awaiting deposit came from (spec 033). Derived for display, never stored.
/// </summary>
public enum ReceivedItemSource
{
    /// <summary>A member payment by cheque or other method.</summary>
    MemberPayment,

    /// <summary>An income entry received by cheque or other method.</summary>
    IncomeEntry,

    /// <summary>The opening balance entered on Undeposited Funds.</summary>
    OpeningBalance
}
