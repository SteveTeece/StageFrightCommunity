namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Represents a single fee eligible for reactivation forgiveness.
/// Prior-year fees are pre-selected by default; the current year is not.
/// </summary>
public class ForgivenessItem
{
    /// <summary>The fee to potentially forgive.</summary>
    public Guid FeeId { get; set; }

    /// <summary>Calendar year the fee was raised in.</summary>
    public int Year { get; set; }

    /// <summary>Date the fee applies to.</summary>
    public DateTime FeeDate { get; set; }

    /// <summary>Original fee amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// True for fees from years before the current calendar year.
    /// UI pre-checks these rows; current-year fees default to unchecked.
    /// </summary>
    public bool IsDefaultForgiven { get; set; }
}
