namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Splits tax-inclusive amounts into net + tax components using the organisation's
/// configured <see cref="Entities.Settings.TaxRate"/>. Users always enter tax-inclusive
/// amounts; the tax component is rounded to the configured currency's minor unit with
/// MidpointRounding.AwayFromZero, and the net is the remainder so the two parts always
/// sum exactly to the gross — at 0, 2 or 3 minor digits alike (spec 028, FR-005).
/// </summary>
public static class TaxCalculator
{
    /// <summary>
    /// Splits a tax-inclusive gross amount into (net, tax) at the given rate.
    /// tax = round(gross * ratePercent / (100 + ratePercent), minorUnitDigits, AwayFromZero); net = gross − tax.
    /// <paramref name="minorUnitDigits"/> defaults to 2, so every call site not passing the configured
    /// currency's precision keeps today's cent rounding unchanged.
    /// </summary>
    public static (decimal Net, decimal Tax) SplitInclusive(decimal gross, decimal ratePercent, int minorUnitDigits = 2)
    {
        var tax = Math.Round(gross * ratePercent / (100m + ratePercent), minorUnitDigits, MidpointRounding.AwayFromZero);
        return (gross - tax, tax);
    }
}
