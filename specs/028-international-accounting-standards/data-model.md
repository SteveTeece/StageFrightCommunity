# Phase 1 Data Model: International accounting-practice readiness

No entity is added and no entity is removed. `Settings` (the singleton, already soft-delete-exempt)
gains three fields and one changed default. Everything else here is a non-persisted type: a value
object, two helpers, one exception, and one guard contract. The append-only ledger — `Fee`,
`Payment`, `Transaction`, `JournalEntry` — is **not touched** (FR-031, FR-032, FR-033).

---

## 1. `Settings` entity — added and changed fields

File: `src/StageFright.Core/Entities/Settings.cs` · EF config:
`src/StageFright.Data/Configurations/SettingsConfiguration.cs` · one row per install.

| Field | Type | Nullability | Default | Rules | Requirement |
|-------|------|-------------|---------|-------|-------------|
| `CurrencyCode` | `string` | non-null | `"AUD"` | Exactly one of `CurrencyCatalog.All` codes (ISO 4217, 3 upper-case letters). Set at first-run setup; **immutable afterward** — `SettingsService.SaveAsync` throws `ValidationException` if an incoming value differs from the persisted one. No currency control on any Settings *edit* surface. | FR-001, FR-002 |
| `FinancialYearStartDay` | `int` | non-null | `1` | Range 1–28 (upper bound avoids month-length edge cases; 52/53-week calendars are out of scope). Combined with the existing `FinancialYearStartMonth` to bound every financial year. | FR-019, FR-020 |
| `ClosedThroughDate` | `DateTime?` | nullable | `null` | `null` = no period closed. When set, any GL posting line dated on or **before** this date (date-inclusive) is rejected. Only ever moves forward in practice; no explicit monotonicity constraint in v1. | FR-016, FR-017 |
| `AuditRetentionYears` | `int` | non-null | **`5`** (was `1`) | Range 1–7 unchanged; still user-configurable. The migration changes only the column default — it does **not** rewrite the existing row (FR-024). | FR-023, FR-024 |

Unchanged and relevant: `FinancialYearStartMonth` (`int`, default `7`) keeps its name and default —
it is a Verbatim Constraint. `LanguageCode`, `Theme`, `IsTaxApplicable`, `TaxRate`,
`AnnualFeeTaxCode`, `AttendanceFeeTaxCode` are untouched.

### Storage / migration

New EF Core migration `<timestamp>_AddInternationalAccountingSettings`:

* `ALTER TABLE Settings ADD COLUMN CurrencyCode TEXT NOT NULL DEFAULT 'AUD';`
* `ALTER TABLE Settings ADD COLUMN FinancialYearStartDay INTEGER NOT NULL DEFAULT 1;`
* `ALTER TABLE Settings ADD COLUMN ClosedThroughDate TEXT NULL;`
* Change the `AuditRetentionYears` column default from `1` to `5` (schema default only — **no
  `migrationBuilder.UpdateData`**).
* `SchemaVersion` bump handled by the existing convention (`Settings.SchemaVersion`, backup manifest).

The `NOT NULL DEFAULT` on the two added non-null columns backfills the single existing row to `AUD` /
day `1` as the column is added — an existing Australian dataset is therefore unchanged (FR-006,
US7 AC-3, SC-004). `ClosedThroughDate` starts `null` (nothing retroactively closed).

### State / lifecycle notes

* **Currency**: `unset → set (at setup) → fixed`. There is no transition out of `set`.
* **Closed-through date**: `null → <date>`, and thereafter `<date> → <later date>`. Setup always
  precedes the first close, so opening balances entered during first-run setup are never blocked
  (FR-018) without any special-casing.

---

## 2. `SupportedCurrency` — value object (new, not persisted)

File: `src/StageFright.Core/Localization/SupportedCurrency.cs`

| Member | Type | Notes |
|--------|------|-------|
| `Code` | `string` | ISO 4217 alphabetic code, e.g. `"AUD"`, `"JPY"`. Identity. |
| `Symbol` | `string` | Display symbol, e.g. `"$"`, `"€"`, `"¥"`. For `AUD` this is `"$"` (Verbatim Constraint). |
| `MinorUnitDigits` | `int` | 0, 2, or 3. Drives display precision and `TaxCalculator` rounding. |
| `DisplayName` | `string` | English name shown in the setup picker, e.g. `"Australian Dollar"`. |

Immutable (`record` or init-only class, one type per file per §3.2.1).

---

## 3. `CurrencyCatalog` — static reference data (new, not persisted)

File: `src/StageFright.Core/Localization/CurrencyCatalog.cs`

* `IReadOnlyList<SupportedCurrency> All` — the curated shipped set. Seed set (representative, not
  exhaustive): `AUD` (2, `$`), `USD` (2, `$`), `EUR` (2, `€`), `GBP` (2, `£`), `NZD` (2, `$`),
  `CAD` (2, `$`), `JPY` (0, `¥`), `KWD` (3, `د.ك`), `BHD` (3, `.د.ب`). Extended by adding a row.
* `bool TryGet(string code, out SupportedCurrency currency)` — case-insensitive.
* `SupportedCurrency Get(string code)` — throws `ValidationException` for an unknown code (used where
  a stored/config value is expected to be valid).
* `SupportedCurrency Default` — the `AUD` entry.

Mirrors `SupportedLanguagesCatalog` (spec 027): a drop-in list with a lookup, no other code change to
add a currency.

---

## 4. `MoneyFormatter` — surface change (existing file, not persisted state)

File: `src/StageFright.Core/Localization/MoneyFormatter.cs`

| Member | Change |
|--------|--------|
| `Configure(SupportedCurrency currency)` | **new** — called once at startup in `MauiProgram` after the display culture is set; stores the currency in an immutable static field. Before it is called (e.g. some tests), the formatter falls back to `CurrencyCatalog.Default` (`AUD`). |
| `Format(decimal amount)` | now uses the configured symbol and `MinorUnitDigits`; grouping, decimal separator and symbol placement still follow `CultureInfo.CurrentCulture`. |
| `FormatWithCode(decimal amount)` | now prefixes the configured ISO code (e.g. `"AUD "`, `"JPY "`). |

No call-site signature changes — the ~20 existing `MoneyFormatter.Format(...)` /
`FormatWithCode(...)` callers compile and behave unchanged for an `AUD` dataset (FR-006).

---

## 5. `MoneyInput` — parse helper (new, not persisted)

File: `src/StageFright.Core/Localization/MoneyInput.cs`

* `static decimal Parse(string? rawValue)` — parses with `CultureInfo.InvariantCulture` and
  `NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign`; returns `0m` for null / blank /
  unparseable, matching the current forms' fallback behaviour.
* Used by `JournalEntryPage` and `OpeningBalanceEntryForm` for the value of an
  `<input type="number">`, which the browser always serialises invariant (Decision 3).
* A repo-wide guard test asserts no money field parses an `<input type="number">` value with
  `CultureInfo.CurrentCulture` (FR-008).

---

## 6. `ClosedPeriodException` — custom exception (new)

File: `src/StageFright.Core/Exceptions/ClosedPeriodException.cs`

`sealed class ClosedPeriodException : Exception`, following the mandated five-member shape
(Constitution §5.2 / domain-model living spec):

| Member | Value |
|--------|-------|
| ctor | `(string message, string entityType, string operationContext, Guid? entityId = null, Exception? innerException = null)` |
| `EntityType` | `"Transaction"` at the GL guard site |
| `EntityId` | `null` (the rejected posting has no persisted id) |
| `OperationContext` | e.g. `"AddBalancedSetAsync"` / `"AddPairAsync"` |
| `Timestamp` | `DateTime.UtcNow` at construction |
| `CorrelationId` | new `Guid` |

Thrown by `ClosedPeriodGuard` (via `GLRepository`) before `SaveChangesAsync`. The UI catches it on
every finance posting form and maps it to a friendly "that date falls in a closed period" message.
`GLBalanceException` remains a distinct signal; `ClosedPeriodException` is not a subclass of it.

---

## 7. `IClosedPeriodGuard` / `ClosedPeriodGuard` — guard contract (new)

Files: `src/StageFright.Core/Contracts/IClosedPeriodGuard.cs`,
`src/StageFright.Core/Modules/Finance/ClosedPeriodGuard.cs`

| Member | Signature | Behaviour |
|--------|-----------|-----------|
| `EnsureOpen` | `Task EnsureOpen(DateTime postingDate, CancellationToken ct = default)` | Loads the `Settings` singleton; if `ClosedThroughDate` is non-null and `postingDate.Date <= ClosedThroughDate.Value.Date`, throws `ClosedPeriodException`; otherwise returns. A null `Settings` (pre-setup) is a no-op. |

Registered in `MauiProgram.RegisterCoreServices`; injected into `GLRepository`. `ClosedPeriodGuard`
depends only on `ISettingsRepository` (no cross-module service dependency).

---

## 8. `ReportData` — added field (existing file, not persisted)

File: `src/StageFright.Reports/Models/ReportData.cs`

| Field | Type | Notes |
|-------|------|-------|
| `BasisOfAccounting` | `string?` (init) | `null` for non-financial reports. When set, rendered by `PdfReportRenderer` (below the generation line), `CsvReportExporter` (trailing labelled note row), and `ReportViewer.razor` (below the subtitle). Set by the financial-statement providers from one shared `ReportsResource` string describing the hybrid accrual/cash basis. |

---

## 9. Setup / request model deltas (not persisted directly)

| File | Added / changed |
|------|-----------------|
| `src/StageFright.UI/Pages/Setup/SetupFormModel.cs` | `+ string CurrencyCode = "AUD"` (required); `+ int FinancialYearStartMonth = 7` (`[Range(1,12)]`); `+ int FinancialYearStartDay = 1` (`[Range(1,28)]`); `AuditRetentionYears` default `1 → 5`. |
| `src/StageFright.Core/Modules/Settings/SetupRequest.cs` | `+ string CurrencyCode = "AUD"`; `+ int FinancialYearStartMonth = 7`; `+ int FinancialYearStartDay = 1`; `AuditRetentionYears` default `1 → 5`. |
| `src/StageFright.Core/Modules/Settings/SetupService.cs` | Validate `CurrencyCode ∈ CurrencyCatalog.All`; validate `FinancialYearStartDay ∈ 1..28`; persist all three onto the new `Settings` fields. |
| `src/StageFright.Core/Modules/Settings/SettingsService.cs` | `SaveAsync` rejects a `CurrencyCode` that differs from the persisted value (`Validation_Settings_CurrencyImmutable`). |

---

## 10. Validation & resource keys touched

* `ValidationResource`: `Validation_Settings_CurrencyImmutable`, `Validation_Setup_CurrencyUnknown`,
  `Validation_Setup_FinancialYearStartDayRange`, `Validation_ClosedPeriod_PostingRejected`
  (+ `.en-US`, `.fr-FR`).
* `ReportsResource`: `Reports_Common_BasisOfAccounting`, `Reports_BalanceSheet_OutOfBalance`,
  `Reports_BankReconciliation_BalancePerBankStatement`,
  `Reports_BankReconciliation_AddOutstandingDeposits`,
  `Reports_BankReconciliation_LessOutstandingPayments`,
  `Reports_BankReconciliation_AdjustedBankBalance`,
  `Reports_BankReconciliation_BalancePerGeneralLedger`,
  `Reports_BankReconciliation_Reconciled` (+ `.en-US`, `.fr-FR`). The existing
  `Reports_TrialBalance_GLImbalanceError` wording is revised to drop the tolerance phrasing.
* `SetupResource` / `SettingsResource`: labels for the currency picker, the financial-year-start
  month/day pickers, and the "close periods through" control (+ `.en-US`, `.fr-FR`).

All new user-facing text is resolved through `IStringLocalizer` per the localization rule — no
hard-coded literals (enforced by `StageFright.Localization.Tests`).
