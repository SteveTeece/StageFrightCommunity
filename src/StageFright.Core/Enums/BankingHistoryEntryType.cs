namespace StageFright.Core.Enums;

/// <summary>
/// The kind of row shown in the Banking history (spec 033). Derived for display, never stored.
/// </summary>
public enum BankingHistoryEntryType
{
    /// <summary>A bank deposit of received items and/or Petty Cash.</summary>
    Deposit,

    /// <summary>A cash withdrawal from a bank account into Petty Cash.</summary>
    Withdrawal
}
