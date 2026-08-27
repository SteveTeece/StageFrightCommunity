using System.Globalization;

namespace StageFright.Core.Localization;

/// <summary>
/// Formats monetary amounts for display. The amount is ALWAYS Australian dollars: the currency
/// symbol/code is fixed ("$" or an explicit "AUD ") regardless of the active culture
/// (FR-015/FR-016). Only the decimal separator, digit grouping and symbol placement follow
/// <see cref="CultureInfo.CurrentCulture"/>. Never use <c>decimal.ToString("C")</c> /
/// <c>"{0:C}"</c> / <c>FormatString="{0:C}"</c> at a display site — that substitutes the active
/// culture's own currency symbol (e.g. "€" under fr-FR), misrepresenting an AUD balance.
/// </summary>
public static class MoneyFormatter
{
    /// <summary>Formats <paramref name="amount"/> with a fixed "$" symbol, e.g. "$1,234.50" (en-AU) / "$1 234,50" (fr-FR).</summary>
    public static string Format(decimal amount) => FormatCore(amount, currencySymbol: "$");

    /// <summary>Formats <paramref name="amount"/> with an explicit "AUD " prefix, e.g. "AUD 1,234.50" — for reports/exports where disambiguation matters.</summary>
    public static string FormatWithCode(decimal amount) => FormatCore(amount, currencySymbol: "AUD ");

    private static string FormatCore(decimal amount, string currencySymbol)
    {
        var format = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        format.CurrencySymbol = currencySymbol;
        return amount.ToString("C", format);
    }
}
