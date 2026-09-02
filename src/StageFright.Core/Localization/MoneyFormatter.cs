using System.Globalization;

namespace StageFright.Core.Localization;

/// <summary>
/// Formats monetary amounts for display in the organisation's configured currency (spec 028,
/// FR-003 / FR-004). The currency symbol, ISO code and minor-unit precision come from the
/// <see cref="SupportedCurrency"/> set once via <see cref="Configure"/> at startup — never from the
/// active culture. Only the decimal separator, digit grouping and symbol placement follow
/// <see cref="CultureInfo.CurrentCulture"/>; a negative amount always uses a leading/trailing minus
/// sign (matching that culture's symbol placement), never accounting-style parentheses — so a stored
/// AUD figure renders "-$42.10" byte-for-byte on any host, whatever the runner's default culture (the
/// invariant culture would otherwise yield "($42.10)"). Before <see cref="Configure"/> is called the formatter
/// behaves as if configured with <see cref="CurrencyCatalog.Default"/> (AUD / "$" / 2 digits), so an
/// AUD dataset is byte-for-byte unchanged (FR-006). Never use <c>decimal.ToString("C")</c> /
/// <c>"{0:C}"</c> / <c>FormatString="{0:C}"</c> at a display site directly — that substitutes the
/// active culture's own currency symbol (e.g. "€" under fr-FR), misrepresenting the amount.
/// </summary>
/// <remarks>
/// The configured currency is process-wide static state, deliberately (plan.md Decision 5 — one
/// currency per install, fixed after setup). Any test that calls <see cref="Configure"/> with a
/// non-default currency MUST restore <see cref="CurrencyCatalog.Default"/> afterwards AND must not
/// run in parallel with a test asserting default (AUD) output — see
/// <c>MoneyFormatterStateCollection</c> in <c>StageFright.Integration.Tests</c> and the
/// <c>Dispose</c> reset in <c>MoneyFormatterTests</c>. A new test assembly that formats money is
/// subject to the same rule as the <see cref="CultureInfo"/> statics it sits alongside.
/// </remarks>
public static class MoneyFormatter
{
    private static SupportedCurrency _currency = CurrencyCatalog.Default;

    /// <summary>
    /// Sets the currency every <see cref="Format"/> / <see cref="FormatWithCode"/> call uses.
    /// Called once at startup (<c>MauiProgram</c>, right after the display culture is applied).
    /// Idempotent — the last call wins. A null argument falls back to <see cref="CurrencyCatalog.Default"/>.
    /// </summary>
    public static void Configure(SupportedCurrency currency) =>
        _currency = currency ?? CurrencyCatalog.Default;

    /// <summary>Formats <paramref name="amount"/> with the configured symbol, e.g. "$1,234.50" (en-AU) / "$1 234,50" (fr-FR) / "¥1,235" (JPY).</summary>
    public static string Format(decimal amount) => FormatCore(amount, _currency.Symbol);

    /// <summary>Formats <paramref name="amount"/> with the configured ISO code prefixed, e.g. "AUD 1,234.50" / "JPY 1,235" — for reports/exports where disambiguation matters.</summary>
    public static string FormatWithCode(decimal amount) => FormatCore(amount, _currency.Code + " ");

    private static string FormatCore(decimal amount, string currencySymbol)
    {
        var format = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        format.CurrencySymbol = currencySymbol;
        format.CurrencyDecimalDigits = _currency.MinorUnitDigits;
        format.CurrencyNegativePattern = MinusSignNegativePattern(format.CurrencyPositivePattern);
        return amount.ToString("C", format);
    }

    /// <summary>
    /// The negative currency pattern with the same symbol placement as the active culture's positive
    /// pattern but a leading/trailing minus sign — never accounting-style parentheses. Keeps a negative
    /// amount visually consistent with a positive one under any culture, and keeps AUD output identical
    /// to the pre-028 "-$42.10" on every host (the invariant culture's default is "($42.10)").
    /// Pattern numbers are the <see cref="NumberFormatInfo.CurrencyNegativePattern"/> table.
    /// </summary>
    private static int MinusSignNegativePattern(int currencyPositivePattern) => currencyPositivePattern switch
    {
        1 => 5,   // "n$"  -> "-n$"
        2 => 9,   // "$ n" -> "-$ n"
        3 => 8,   // "n $" -> "-n $"
        _ => 1,   // "$n"  -> "-$n"  (positive pattern 0, and any unexpected value)
    };
}
