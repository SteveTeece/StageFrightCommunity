using StageFright.Core.Exceptions;

namespace StageFright.Core.Localization;

/// <summary>
/// Curated set of currencies an organisation can keep its books in (spec 028, FR-001). Mirrors
/// <c>SupportedLanguagesCatalog</c> (spec 027): a drop-in list with a lookup — adding a currency is
/// one new <see cref="SupportedCurrency"/> row here and no other code change. The set is
/// representative, not the full ~180-entry ISO 4217 table: it deliberately spans 0-, 2- and
/// 3-minor-digit currencies so the rounding and display paths are all exercised.
/// </summary>
public static class CurrencyCatalog
{
    /// <summary>The shipped currencies, in a stable order with <c>AUD</c> first (the setup default).</summary>
    public static IReadOnlyList<SupportedCurrency> All { get; } =
    [
        new("AUD", "$", 2, "Australian Dollar"),
        new("USD", "$", 2, "US Dollar"),
        new("EUR", "€", 2, "Euro"),
        new("GBP", "£", 2, "Pound Sterling"),
        new("NZD", "$", 2, "New Zealand Dollar"),
        new("CAD", "$", 2, "Canadian Dollar"),
        new("JPY", "¥", 0, "Japanese Yen"),
        new("KWD", "د.ك", 3, "Kuwaiti Dinar"),
        new("BHD", ".د.ب", 3, "Bahraini Dinar"),
    ];

    /// <summary>The <c>AUD</c> entry — <c>Code = "AUD"</c>, <c>Symbol = "$"</c>, <c>MinorUnitDigits = 2</c>. The pre-configuration fallback.</summary>
    public static SupportedCurrency Default { get; } = All[0];

    /// <summary>
    /// Case-insensitive lookup by ISO 4217 code. Returns <c>false</c> and hands back
    /// <see cref="Default"/> on a miss — never throws. Use where an unknown code is a normal
    /// outcome (e.g. resolving a possibly-stale stored value at startup).
    /// </summary>
    public static bool TryGet(string code, out SupportedCurrency currency)
    {
        currency = Default;

        if (string.IsNullOrWhiteSpace(code))
            return false;

        var match = All.FirstOrDefault(c => string.Equals(c.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return false;

        currency = match;
        return true;
    }

    /// <summary>
    /// Case-insensitive lookup by ISO 4217 code. Throws <see cref="ValidationException"/> for an
    /// unknown code — use where the value is expected to be one the catalog knows (a validated
    /// setup choice, a stored <c>Settings.CurrencyCode</c>).
    /// </summary>
    public static SupportedCurrency Get(string code)
    {
        if (TryGet(code, out var currency))
            return currency;

        throw new ValidationException(
            $"Unknown currency code '{code}'.",
            entityType: nameof(SupportedCurrency),
            operationContext: nameof(CurrencyCatalog) + "." + nameof(Get));
    }
}
