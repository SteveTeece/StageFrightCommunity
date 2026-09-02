# Contract: Settings & first-run setup

Covers US1 currency selection (FR-001, FR-002), US7 financial-year start (FR-019…021), and US8 audit
retention (FR-023, FR-024). Verbatim identifiers used exactly: **`AUD`**, **`ISO 4217`**,
**`FinancialYearStartMonth`**, **`AuditRetentionYears`**.

---

## `Settings` entity fields (see data-model.md for storage detail)

| Field | Type | Default | Editable after setup? |
|-------|------|---------|-----------------------|
| `CurrencyCode` | `string` (`ISO 4217`) | `"AUD"` | **No** — fixed for the life of the dataset (FR-002). |
| `FinancialYearStartMonth` | `int` | `7` | Yes (existing behaviour, existing settings control). Name unchanged (Verbatim Constraint). |
| `FinancialYearStartDay` | `int` (1–28) | `1` | Yes. New; paired with `FinancialYearStartMonth`. |
| `ClosedThroughDate` | `DateTime?` | `null` | Yes — set/advanced from the settings "close periods" control (see period-lock-contract.md). |
| `AuditRetentionYears` | `int` (1–7) | **`5`** (was `1`) | Yes (existing control). Name unchanged (Verbatim Constraint). |

## `SetupRequest` (record) — added parameters

`src/StageFright.Core/Modules/Settings/SetupRequest.cs`

```
+ string CurrencyCode = "AUD"
+ int    FinancialYearStartMonth = 7
+ int    FinancialYearStartDay = 1
  int    AuditRetentionYears = 5      // default changed from 1
```

All added as trailing optional parameters (record positional list), preserving existing call
compatibility. `CurrencyCode` defaults to `"AUD"`.

## `SetupFormModel` — added properties

`src/StageFright.UI/Pages/Setup/SetupFormModel.cs`

| Property | Attribute | Default |
|----------|-----------|---------|
| `string CurrencyCode` | `[Required]` | `"AUD"` |
| `int FinancialYearStartMonth` | `[Range(1, 12)]` | `7` |
| `int FinancialYearStartDay` | `[Range(1, 28)]` | `1` |
| `int AuditRetentionYears` | `[Range(1, 7)]` (unchanged) | `5` (was `1`) |

## `SetupService.InitializeAsync` — validation contract

`src/StageFright.Core/Modules/Settings/SetupService.cs`

* Reject the request (no `Settings` row created) when:
  * `CurrencyCode` is not one of `CurrencyCatalog.All` codes → `ValidationException`
    (`Validation_Setup_CurrencyUnknown`).
  * `FinancialYearStartMonth` is outside `1..12` → `ValidationException`
    (`Validation_Setup_FinancialYearStartMonthRange`).
  * `FinancialYearStartDay` is outside `1..28` → `ValidationException`
    (`Validation_Setup_FinancialYearStartDayRange`).
* On success, persist `CurrencyCode`, `FinancialYearStartMonth`, `FinancialYearStartDay`,
  `AuditRetentionYears` onto the new `Settings` singleton.
* First-run setup MUST require a currency choice — the picker is a mandatory, always-visible control
  defaulting to `AUD` (FR-001).
* The financial-year start MUST be an explicit choice — month + day pickers are mandatory,
  always-visible, defaulting to month `7` / day `1`; there is no hidden default path (FR-019).

## `SettingsService.SaveAsync` — currency immutability

`src/StageFright.Core/Modules/Settings/SettingsService.cs`

* Before persisting, if the persisted `Settings.CurrencyCode` is non-empty and the incoming
  `CurrencyCode` differs from it → throw `ValidationException`
  (`Validation_Settings_CurrencyImmutable`); nothing is persisted.
* No Settings *edit* tab renders a currency control (FR-002).

## Setup UI — element identifiers a test codes against

Added to the organisation-settings tab composite (`GeneralAppearanceTab`); no new wizard tab index.

| Element | id | Type |
|---------|-----|------|
| Currency picker | `setup-currency` | `<select>` bound to `SetupFormModel.CurrencyCode`, options from `CurrencyCatalog.All` (`DisplayName` + `Code`). |
| Financial-year start month | `setup-fy-start-month` | `<select>` 1–12 bound to `FinancialYearStartMonth`. |
| Financial-year start day | `setup-fy-start-day` | `<select>` 1–28 bound to `FinancialYearStartDay`. |
| Audit retention years | `setup-audit-retention` (existing) | unchanged control; default now shows `5`. |

## Reporting behaviour driven by these settings

* Every `FinancialYearCalculator.GetRange` / `GetPreviousRange` caller passes
  `settings.FinancialYearStartDay` alongside `settings.FinancialYearStartMonth`; all FY-preset
  reports and dashboard figures then bound the year on month **and** day (FR-021).
* An existing dataset with month `7`, day `1` produces byte-identical report ranges (US7 AC-3,
  SC-004).
