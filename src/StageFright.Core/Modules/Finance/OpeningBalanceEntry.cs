namespace StageFright.Core.Modules.Finance;

/// <summary>
/// One account's opening balance as entered in the opening balances wizard.
/// A positive amount posts to the account's normal side (debit for Asset/Expense,
/// credit for Liability/Equity/Income); a negative amount posts to the opposite side.
/// </summary>
public class OpeningBalanceEntry
{
    /// <summary>Id of the account. Member Receivable, GST accounts, and Opening Balance Equity are excluded.</summary>
    public Guid AccountId { get; set; }

    /// <summary>The opening balance. Zero entries are skipped.</summary>
    public decimal Amount { get; set; }
}
