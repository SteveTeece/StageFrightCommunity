namespace StageFright.Core.Localization;

/// <summary>
/// One currency the application supports for an organisation's books (spec 028, FR-001).
/// Curated in <see cref="CurrencyCatalog"/>, chosen once at first-run setup, and fixed for the
/// life of the dataset (FR-002). Never persisted as an entity — only <c>Settings.CurrencyCode</c>
/// (the <see cref="Code"/>) is stored.
/// </summary>
/// <param name="Code">ISO 4217 alphabetic code — three upper-case letters, e.g. <c>"AUD"</c>, <c>"JPY"</c>. Identity.</param>
/// <param name="Symbol">Display symbol, e.g. <c>"$"</c> (AUD, a Verbatim Constraint), <c>"€"</c>, <c>"¥"</c>.</param>
/// <param name="MinorUnitDigits">ISO 4217 minor-unit exponent — 0, 2, or 3 across the supported set. Drives display precision and <c>TaxCalculator</c> rounding.</param>
/// <param name="DisplayName">English name shown in the setup picker, e.g. <c>"Australian Dollar"</c>.</param>
public sealed record SupportedCurrency(
    string Code,
    string Symbol,
    int MinorUnitDigits,
    string DisplayName);
