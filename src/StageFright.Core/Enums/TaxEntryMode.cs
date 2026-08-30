namespace StageFright.Core.Enums;

/// <summary>
/// How the organisation enters a taxable amount (<see cref="Entities.Settings.TaxEntryMode"/>).
/// Interprets a <em>newly entered</em> figure only — stored fees, payments and transactions keep
/// the amounts they were posted with (spec 028, issue #354).
/// </summary>
public enum TaxEntryMode
{
    /// <summary>
    /// The entered figure is the tax-inclusive gross; the tax component is split back out of it
    /// (<see cref="Modules.Finance.TaxCalculator.SplitInclusive"/>). The default, and every
    /// pre-#354 dataset's behaviour.
    /// </summary>
    Inclusive,

    /// <summary>
    /// The entered figure is the net; tax is added on top
    /// (<see cref="Modules.Finance.TaxCalculator.SplitExclusive"/>) to reach the gross. How US
    /// sales tax and similar tax-exclusive conventions quote a price ("$100 + 8% = $108").
    /// </summary>
    Exclusive
}
