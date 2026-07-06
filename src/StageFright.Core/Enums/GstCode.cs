namespace StageFright.Core.Enums;

/// <summary>
/// GST treatment of a transaction or fee, following Australian BAS terminology.
/// Stamped at posting/accrual time; historical rows keep the code then in force.
/// </summary>
public enum GstCode
{
    /// <summary>Taxable supply/acquisition — 1/11th of the gross amount is GST.</summary>
    Gst,

    /// <summary>GST-free supply (BAS G3). No GST component.</summary>
    GstFree,

    /// <summary>Input-taxed supply. No GST component; reported within G1 sales.</summary>
    InputTaxed,

    /// <summary>Outside the BAS entirely (transfers, journals, opening balances).</summary>
    BasExcluded
}
