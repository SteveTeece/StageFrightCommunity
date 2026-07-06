namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Input model for posting opening balances. Any residual difference is plugged
/// to the Opening Balance Equity account (3100).
/// </summary>
public class RecordOpeningBalancesRequest
{
    /// <summary>UTC date the opening balances are recorded as at (typically the FY start).</summary>
    public DateTime AsAtDate { get; set; }

    /// <summary>The entered balances. Zero entries are skipped; at least one non-zero entry required.</summary>
    public List<OpeningBalanceEntry> Entries { get; set; } = new();
}
