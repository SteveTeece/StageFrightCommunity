namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Statutory Australian GST figures. Not user-editable — a rate change is a code change.
/// </summary>
public static class GstConstants
{
    /// <summary>The GST rate (10%).</summary>
    public const decimal Rate = 0.10m;

    /// <summary>Divisor extracting the GST component from a GST-inclusive amount (gross / 11).</summary>
    public const decimal InclusiveDivisor = 11m;
}
