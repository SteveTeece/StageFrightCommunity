namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Splits GST-inclusive amounts into net + GST components.
/// Users always enter GST-inclusive amounts; the GST component is gross/11 rounded
/// to the cent with MidpointRounding.AwayFromZero, and the net is the remainder so
/// the two parts always sum exactly to the gross.
/// </summary>
public static class GstCalculator
{
    /// <summary>
    /// Splits a GST-inclusive gross amount into (net, gst).
    /// gst = round(gross / 11, 2, AwayFromZero); net = gross − gst.
    /// </summary>
    public static (decimal Net, decimal Gst) SplitInclusive(decimal gross)
    {
        var gst = Math.Round(gross / GstConstants.InclusiveDivisor, 2, MidpointRounding.AwayFromZero);
        return (gross - gst, gst);
    }
}
