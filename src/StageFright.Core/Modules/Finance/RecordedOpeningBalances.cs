namespace StageFright.Core.Modules.Finance;

/// <summary>
/// The recorded opening balances, shown read-only on Finance → Opening Balances (issue #398).
/// They are recorded once, at setup, and locked: a correction is a journal entry.
/// </summary>
public class RecordedOpeningBalances
{
    /// <summary>
    /// The as-at date the balances were recorded on; null when none were ever recorded (a dataset
    /// from before issue #398, or restored from one), where every account opened at zero.
    /// </summary>
    public DateTime? AsAtDate { get; set; }

    /// <summary>
    /// One line per eligible account — zero where it had no opening balance — plus any account
    /// posted to that is no longer eligible, ordered by account number. Opening Balance Equity
    /// is <see cref="Plug"/>, not a line.
    /// </summary>
    public IReadOnlyList<RecordedOpeningBalanceLine> Lines { get; set; } = [];

    /// <summary>The amount posted to Opening Balance Equity (3100): positive = credit, negative = debit.</summary>
    public decimal Plug { get; set; }
}
