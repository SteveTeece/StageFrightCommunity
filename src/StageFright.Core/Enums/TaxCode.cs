namespace StageFright.Core.Enums;

/// <summary>
/// Tax treatment of a transaction or fee under the organisation's configured sales tax
/// (<see cref="Entities.Settings.TaxRate"/>). Stamped at posting/accrual time; historical
/// rows keep the code then in force.
/// </summary>
public enum TaxCode
{
    /// <summary>Taxable supply/acquisition — a tax component is split out at the configured rate.</summary>
    Taxable,

    /// <summary>Tax-exempt supply. No tax component.</summary>
    TaxExempt,

    /// <summary>Outside tax reporting entirely (transfers, journals, opening balances).</summary>
    Excluded
}
