# Contract: Currency formatting & money input

Covers US1 (FR-001…006, FR-031) and US2 (FR-007…009). Consumers: every `MoneyFormatter` call site in
`StageFright.UI` and `StageFright.Core`, every `StageFright.Reports` provider, and the two money-entry
forms. Verbatim identifiers from the spec are used exactly: **`AUD`**, **`$`**, **`ISO 4217`**.

---

## `SupportedCurrency` (value object)

`src/StageFright.Core/Localization/SupportedCurrency.cs`

```csharp
public sealed record SupportedCurrency(
    string Code,            // ISO 4217 alphabetic, 3 upper-case letters, e.g. "AUD"
    string Symbol,          // e.g. "$" for AUD
    int MinorUnitDigits,    // 0, 2, or 3
    string DisplayName);    // English, e.g. "Australian Dollar"
```

* Identity is `Code`. `Symbol` for `AUD` is exactly `$`.
* `MinorUnitDigits` is the ISO 4217 minor-unit exponent; only 0, 2, 3 occur in the supported set.

## `CurrencyCatalog` (static reference data)

`src/StageFright.Core/Localization/CurrencyCatalog.cs`

| Member | Signature | Behaviour |
|--------|-----------|-----------|
| `All` | `static IReadOnlyList<SupportedCurrency> All` | The curated shipped set, stable order (AUD first). |
| `Default` | `static SupportedCurrency Default` | The `AUD` entry — `Code = "AUD"`, `Symbol = "$"`, `MinorUnitDigits = 2`. |
| `TryGet` | `static bool TryGet(string code, out SupportedCurrency currency)` | Case-insensitive; `false` + `Default` on miss. |
| `Get` | `static SupportedCurrency Get(string code)` | Case-insensitive; throws `ValidationException` on an unknown code. |

Seed `All` (representative — extended by adding a row, no other code change):

| Code | Symbol | MinorUnitDigits |
|------|--------|-----------------|
| `AUD` | `$` | 2 |
| `USD` | `$` | 2 |
| `EUR` | `€` | 2 |
| `GBP` | `£` | 2 |
| `NZD` | `$` | 2 |
| `CAD` | `$` | 2 |
| `JPY` | `¥` | 0 |
| `KWD` | `د.ك` | 3 |
| `BHD` | `.د.ب` | 3 |

## `MoneyFormatter` (existing static — surface additions)

`src/StageFright.Core/Localization/MoneyFormatter.cs`

| Member | Signature | Contract |
|--------|-----------|----------|
| `Configure` | `static void Configure(SupportedCurrency currency)` | Called once at startup (`MauiProgram`, after the display culture is applied). Idempotent; last call wins. Stores an immutable reference. |
| `Format` | `static string Format(decimal amount)` | Returns the amount with the **configured** currency `Symbol` prefixed/placed per `CultureInfo.CurrentCulture`, and exactly `MinorUnitDigits` fractional digits. Grouping and decimal separators follow `CultureInfo.CurrentCulture`. Never emits `"C"` / `"{0:C}"`. |
| `FormatWithCode` | `static string FormatWithCode(decimal amount)` | As `Format`, but with the configured **`ISO 4217`** code and a trailing space instead of the symbol, e.g. `"AUD 1,234.50"`, `"JPY 1,235"`. |

Guarantees:

* Before `Configure` is called, both methods behave as if configured with `CurrencyCatalog.Default`
  (an `AUD` / `$` / 2-digit dataset is byte-for-byte unchanged — FR-006).
* No screen, report, PDF, or CSV emits a symbol or code other than the configured currency's
  (FR-004) — all money display routes through these two methods.
* For a 0-decimal currency (e.g. `JPY`) no fractional digits are shown and figures still reconcile
  exactly (FR-003, Edge Cases).

## `MoneyInput` (new parse helper)

`src/StageFright.Core/Localization/MoneyInput.cs`

| Member | Signature | Contract |
|--------|-----------|----------|
| `Parse` | `static decimal Parse(string? rawValue)` | Parses with `CultureInfo.InvariantCulture` and `NumberStyles.AllowDecimalPoint \| NumberStyles.AllowLeadingSign`. Returns `0m` for null, blank, or unparseable input. |

Rationale: the value of an `<input type="number">` is always invariant-formatted by the browser,
independent of page locale. Parsing it invariant makes the manual journal and opening-balance forms
interpret an amount identically to every other money field (FR-008) and removes any
thousands-separator ambiguity (FR-009).

### Call-site changes

| File | Before | After |
|------|--------|-------|
| `src/StageFright.UI/Pages/Finance/JournalEntryPage.razor.cs` — `ParseAmount` | `decimal.TryParse(v, NumberStyles.Number, CultureInfo.CurrentCulture, …)` | `MoneyInput.Parse(v)` |
| `src/StageFright.UI/Shared/OpeningBalanceEntryForm.razor.cs` — `SetAmount` | `decimal.TryParse(v, NumberStyles.Number, CultureInfo.CurrentCulture, …)` | `MoneyInput.Parse(v)` |

Both inputs remain `type="number"`. A `StageFright.Localization.Tests` guard asserts no money field
parses a numeric-input value with `CultureInfo.CurrentCulture`.

## Rounding — `TaxCalculator.SplitInclusive`

`src/StageFright.Core/Modules/Finance/TaxCalculator.cs`

* Rounds the tax component to the **configured currency's `MinorUnitDigits`** (not a literal `2`).
* `net = gross − tax` remains the remainder, so `net + tax == gross` exactly at 0, 2, or 3 digits
  (FR-005). No stored tax value changes for an `AUD` dataset (FR-033).

## Reports

Each provider's private `FormatCurrency` (currently `amount.ToString("F2")`) routes through
`MoneyFormatter.Format` / `FormatWithCode`, so printed and exported amounts carry the configured
symbol and precision (FR-003) and never a mismatched one (FR-004).
