using System.Globalization;

namespace StageFright.Core.Localization;

/// <summary>
/// Formats monetary amounts for display in the organisation's configured currency (spec 028,
/// FR-003 / FR-004). The currency symbol, ISO code and minor-unit precision come from the
/// <see cref="SupportedCurrency"/> set once via <see cref="Configure"/> at startup — never from the
/// active culture. Only the decimal separator, digit grouping and symbol placement follow
/// <see cref="CultureInfo.CurrentCulture"/>. Before <see cref="Configure"/> is called the formatter
/// behaves as if configured with <see cref="CurrencyCatalog.Default"/> (AUD / "$" / 2 digits), so an
/// AUD dataset is byte-for-byte unchanged (FR-006). Never use <c>decimal.ToString("C")</c> /
/// <c>"{0:C}"</c> / <c>FormatString="{0:C}"</c> at a display site directly — that substitutes the
/// active culture's own currency symbol (e.g. "€" under fr-FR), misrepresenting the amount.
/// </summary>
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
        return amount.ToString("C", format);
    }
}
