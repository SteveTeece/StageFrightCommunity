namespace StageFright.Core.Modules.Finance;

/// <summary>
/// The opening balances already posted, as shown read-only on Finance → Opening Balances
/// once they exist (issue #398). They are locked: an adjustment is a general journal.
/// </summary>
public class RecordedOpeningBalances
{
    /// <summary>The as-at date the balances were posted on.</summary>
    public DateTime AsAtDate { get; set; }

    /// <summary>One line per account posted to, ordered by account number. Opening Balance Equity is <see cref="Plug"/>, not a line.</summary>
    public IReadOnlyList<RecordedOpeningBalanceLine> Lines { get; set; } = [];

    /// <summary>The amount posted to Opening Balance Equity (3100): positive = credit, negative = debit.</summary>
    public decimal Plug { get; set; }
}
