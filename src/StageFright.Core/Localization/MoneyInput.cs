using System.Globalization;

namespace StageFright.Core.Localization;

/// <summary>
/// Parses the raw string value of an <c>&lt;input type="number"&gt;</c> money field into a
/// <see cref="decimal"/> (spec 028, FR-007…FR-009). A number input always serialises its value
/// with <see cref="CultureInfo.InvariantCulture"/> — a period decimal point and no digit
/// grouping — regardless of the page or device region, so the value is parsed invariant here
/// too. Parsing it with <see cref="CultureInfo.CurrentCulture"/> is the US2 data-corruption bug:
/// under fr-FR / de-DE the period is read as a thousands separator and <c>1.50</c> posts as
/// <c>150</c>. A null, blank, or unparseable value yields <c>0m</c> — the same "empty field is
/// zero" behaviour the hand-rolled parsers had. Every money-entry field routes typed amounts
/// through this one helper so they are all interpreted identically (FR-008).
/// </summary>
public static class MoneyInput
{
    private const NumberStyles Styles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;

    /// <summary>
    /// Invariant-parses <paramref name="value"/> (the browser-serialised value of a numeric
    /// input). Returns <c>0m</c> for null, blank, or unparseable input.
    /// </summary>
    public static decimal Parse(string? value) =>
        decimal.TryParse(value, Styles, CultureInfo.InvariantCulture, out var amount) ? amount : 0m;
}
