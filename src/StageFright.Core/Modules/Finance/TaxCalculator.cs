namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Splits tax-inclusive amounts into net + tax components using the organisation's
/// configured <see cref="Entities.Settings.TaxRate"/>. Users always enter tax-inclusive
/// amounts; the tax component is rounded to the cent with MidpointRounding.AwayFromZero,
/// and the net is the remainder so the two parts always sum exactly to the gross.
/// </summary>
public static class TaxCalculator
{
    /// <summary>
    /// Splits a tax-inclusive gross amount into (net, tax) at the given rate.
    /// tax = round(gross * ratePercent / (100 + ratePercent), 2, AwayFromZero); net = gross − tax.
    /// </summary>
    public static (decimal Net, decimal Tax) SplitInclusive(decimal gross, decimal ratePercent)
    {
        var tax = Math.Round(gross * ratePercent / (100m + ratePercent), 2, MidpointRounding.AwayFromZero);
        return (gross - tax, tax);
    }
}
