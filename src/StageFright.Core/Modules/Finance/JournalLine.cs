namespace StageFright.Core.Modules.Finance;

/// <summary>
/// One line of a manual general journal. Exactly one of DebitAmount / CreditAmount
/// must be non-zero and positive.
/// </summary>
public class JournalLine
{
    /// <summary>Id of the account this line posts to. Member Receivable is blocked.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Debit amount. Zero when the line is a credit.</summary>
    public decimal DebitAmount { get; set; }

    /// <summary>Credit amount. Zero when the line is a debit.</summary>
    public decimal CreditAmount { get; set; }
}
