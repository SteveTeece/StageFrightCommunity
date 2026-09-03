using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Splits a taxable amount into net + tax components using the organisation's configured
/// <see cref="Entities.Settings.TaxRate"/>. The amount is interpreted per the organisation's
/// <see cref="Entities.Settings.TaxEntryMode"/>: <see cref="TaxEntryMode.Inclusive"/> (the
/// default) treats it as the tax-inclusive gross and splits tax back out; <see cref="TaxEntryMode.Exclusive"/>
/// treats it as the net and adds tax on top (spec 028, FR-005 / issue #354). Either way the tax
/// component is rounded to the configured currency's minor unit with MidpointRounding.AwayFromZero
/// and net + tax sum exactly to the gross — at 0, 2 or 3 minor digits alike.
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

    /// <summary>
    /// Splits a tax-exclusive net amount into (gross, tax) at the given rate.
    /// tax = round(net * ratePercent / 100, minorUnitDigits, AwayFromZero); gross = net + tax, so the
    /// two parts re-sum to the gross exactly. <paramref name="minorUnitDigits"/> defaults to 2.
    /// </summary>
    public static (decimal Gross, decimal Tax) SplitExclusive(decimal net, decimal ratePercent, int minorUnitDigits = 2)
    {
        var tax = Math.Round(net * ratePercent / 100m, minorUnitDigits, MidpointRounding.AwayFromZero);
        return (net + tax, tax);
    }

    /// <summary>
    /// Resolves an entered amount to its (gross, net, tax) parts under the organisation's
    /// <paramref name="mode"/> — the single call site every taxable posting service uses:
    /// <see cref="TaxEntryMode.Inclusive"/> feeds <see cref="SplitInclusive"/> (entered = gross);
    /// <see cref="TaxEntryMode.Exclusive"/> feeds <see cref="SplitExclusive"/> (entered = net).
    /// gross always equals net + tax.
    /// </summary>
    public static (decimal Gross, decimal Net, decimal Tax) Split(
        decimal enteredAmount, TaxEntryMode mode, decimal ratePercent, int minorUnitDigits = 2)
    {
        if (mode == TaxEntryMode.Exclusive)
        {
            var (gross, tax) = SplitExclusive(enteredAmount, ratePercent, minorUnitDigits);
            return (gross, enteredAmount, tax);
        }

        var (net, incTax) = SplitInclusive(enteredAmount, ratePercent, minorUnitDigits);
        return (enteredAmount, net, incTax);
    }
}
