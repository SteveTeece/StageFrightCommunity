# Phase 1 Data Model: International accounting-practice readiness

No entity is added and no entity is removed. `Settings` (the singleton, already soft-delete-exempt)
gains five fields (`InceptionDate` via the Phase 14 follow-on migration for issue #353, `TaxEntryMode`
via the Phase 15 follow-on migration for issue #354) and one changed default. Everything else here is
a non-persisted type: a value object, two helpers, one exception, one guard contract, (Phase 14) a
first-period-aware `FinancialYearCalculator` overload, and (Phase 15) `TaxCalculator.SplitExclusive` /
`TaxCalculator.Split(mode)`. Phase 16 (issue #355) adds no field at all — it re-types one seeded
system account (`2320`, recoverable input tax) from `Liability` to `Asset` via a data migration. The
append-only ledger — `Fee`, `Payment`, `Transaction`, `JournalEntry` — is **not touched** (FR-031,
FR-032, FR-033).

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
| `InceptionDate` | `DateTime?` | nullable | `null` | Optional organisation founding date, captured at first-run setup (Phase 14 / issue #353). `null` on every dataset created before it was offered. When later than the `(FinancialYearStartMonth, FinancialYearStartDay)` anchor, the first financial year opens on this date and every FY-preset report labels it a part-year; later years are full twelve-month periods. Presentation / range calculation only — no stored amount changes. | FR-022 |
| `TaxEntryMode` | `TaxEntryMode` enum (`Inclusive` \| `Exclusive`) | non-null | `Inclusive` | How a newly entered taxable amount is interpreted (Phase 15 / issue #354). `Inclusive` (every pre-#354 dataset) = the entered figure is the tax-inclusive gross, split via `TaxCalculator.SplitInclusive`. `Exclusive` = the entered figure is the net; tax is added on top via `TaxCalculator.SplitExclusive` and the receivable/bank line (and `Fee.Amount` / `Payment.Amount`) carry the gross. Only meaningful while `IsTaxApplicable`; `SettingsService.SaveAsync` / `SetupService` force it to `Inclusive` when tax is off. Stored as its member name string (`HasConversion<string>()`). No stored monetary or `TaxCode` value changes. | FR-029, FR-030 (issue #354) |

Unchanged and relevant: `FinancialYearStartMonth` (`int`, default `7`) keeps its name and default —
it is a Verbatim Constraint. `LanguageCode`, `Theme`, `IsTaxApplicable`, `TaxRate`,
`AnnualFeeTaxCode`, `AttendanceFeeTaxCode` are untouched (the Phase 15 `TaxEntryMode` field is
*added* alongside them, never altering their values or the tax-inclusive posting path).

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

A follow-on migration `<timestamp>_AddOrganisationInceptionDate` (spec 028 Phase 14 / issue #353),
added separately because the rest of spec 028 had already shipped, adds:

* `ALTER TABLE Settings ADD COLUMN InceptionDate TEXT NULL;`

It starts `null`, so an existing dataset keeps a full twelve-month first year with no part-year label
(SC-014).

A further follow-on migration `<timestamp>_AddTaxEntryMode` (spec 028 Phase 15 / issue #354) adds:

* `ALTER TABLE Settings ADD COLUMN TaxEntryMode TEXT NOT NULL DEFAULT 'Inclusive';`

The `NOT NULL DEFAULT 'Inclusive'` backfills the single existing row as the column is added, so every
pre-#354 dataset keeps today's tax-inclusive entry behaviour and stays byte-identical (SC-015,
`AddTaxEntryModeMigrationTests`).

A further follow-on migration `<timestamp>_ReclassifyInputTaxAsReceivable` (spec 028 Phase 16 / issue
#355) re-types one seeded row — no schema change:

* `UPDATE Accounts SET Name = 'Tax Receivable', Type = 'Asset' WHERE Id = '…0005';`
  (`migrationBuilder.UpdateData` on `Name` + `Type`; `Down` restores `'Tax Paid'` / `'Liability'`).

The account number stays `2320`; `2310` "Tax Collected" is untouched. No monetary amount, no
`TaxCode`, no ledger line moves, so an `AUD` dataset's reports and stored values are byte-identical
(SC-016, FR-031, `ReclassifyInputTaxAsReceivableMigrationTests`).

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
| `src/StageFright.UI/Pages/Setup/SetupFormModel.cs` | `+ string CurrencyCode = "AUD"` (required); `+ int FinancialYearStartMonth = 7` (`[Range(1,12)]`); `+ int FinancialYearStartDay = 1` (`[Range(1,28)]`); `AuditRetentionYears` default `1 → 5`. Phase 14: `+ DateTime? InceptionDate` (optional, no `[Required]`). |
| `src/StageFright.Core/Modules/Settings/SetupRequest.cs` | `+ string CurrencyCode = "AUD"`; `+ int FinancialYearStartMonth = 7`; `+ int FinancialYearStartDay = 1`; `AuditRetentionYears` default `1 → 5`. Phase 14: `+ DateTime? InceptionDate = null` (trailing). |
| `src/StageFright.Core/Modules/Settings/SetupService.cs` | Validate `CurrencyCode ∈ CurrencyCatalog.All`; validate `FinancialYearStartDay ∈ 1..28`; persist all three onto the new `Settings` fields. Phase 14: persist `InceptionDate = request.InceptionDate?.Date`. |
| `src/StageFright.UI/Pages/Setup/Tabs/GeneralAppearanceTab.razor` | Phase 14: optional `#setup-inception-date` `<InputDate>` bound to `SetupFormModel.InceptionDate` (no required marker). |
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
  month/day pickers, and the "close periods through" control (+ `.en-US`, `.fr-FR`). Phase 14 adds
  `Setup_General_InceptionDateLabel` / `Setup_General_InceptionDateHelp` (+ `.en-US`, `.fr-FR`).
* `ReportsResource` (Phase 14): `Reports_Common_PartYearSubtitle` — `{Period} (part-year — first
  financial year)`, wrapping a statement subtitle when the default FY-preset period is the
  sub-twelve-month first financial year (+ `.en-US`, `.fr-FR`; `qps-ploc` regenerated).
* `SharedResource`: `Shared_StartupWarning_AuditPurgeFailed`, `Shared_StartupWarning_DismissLabel`
  (+ `.en-US`, `.fr-FR`) — the dismissible dashboard banner shown when the startup audit-trail
  purge failed (FR-025).

`IStartupDiagnosticService` (existing, `StageFright.Core/Contracts/`) gains a **non-fatal** warning
channel — `HasStartupWarning`, `StartupWarning`, `RecordWarning(string)` — alongside its existing
fatal error channel. A failed startup audit purge is recorded here (by `MauiProgram`) and surfaced
as the dashboard banner above; unlike `RecordError`, it never routes the user to the blocking
`/startup-error` recovery page, and startup still completes (FR-025). `AuditTrailService.PurgeOlderThanAsync`
no longer swallows a purge failure — it propagates so the startup sequence can log **and** surface it.

All new user-facing text is resolved through `IStringLocalizer` per the localization rule — no
hard-coded literals (enforced by `StageFright.Localization.Tests`).

---

## 11. Phase 14 — sub-twelve-month first financial year (FR-022 / issue #353), not persisted

* **`FinancialYearCalculator`** (`src/StageFright.Core/Modules/Finance/`) gains first-period-aware
  overloads: `GetRange(date, startMonth, startDay, DateTime? inceptionDate)` and
  `GetPreviousRange(...)`, each returning `(DateTime From, DateTime To, bool IsPartYear)`. A `null`
  inception date reproduces the existing 3-arg result with `IsPartYear == false`. The first period
  is a part-year iff `inceptionDate.Date` is strictly after that financial year's normal opening
  anchor and on or before its end; then `From` opens on the inception date. Every later year, and an
  inception date on the anchor, is a full twelve months. `GetPreviousRange` pivots on the current
  year's *un-clamped* anchor so it never collapses onto the part-year period.
* **`PartYearSubtitle`** (`src/StageFright.Reports/Providers/`, `internal static`) — `Wrap(localizer,
  subtitle, isPartYear)` returns the subtitle wrapped via `Reports_Common_PartYearSubtitle` when
  `isPartYear`, else the subtitle unchanged.
* **`TrialBalanceReportProvider`, `IncomeStatementReportProvider`, `TaxSummaryReportProvider`,
  `BalanceSheetReportProvider`** pass `settings?.InceptionDate` into `FinancialYearCalculator` and
  wrap their subtitle via `PartYearSubtitle.Wrap` when the default FY-preset period (no user date
  override) is the part-year period. The as-at date, all monetary figures, and the integrity checks
  are unchanged (FR-031, FR-032).
* The dashboard finance tile carries only month-to-date / inception-to-date figures — no FY-preset
  figure — so there is no part-year surface there.

---

## 12. Phase 15 — tax-exclusive amount entry (issue #354), `Settings.TaxEntryMode` + calculator

* **`TaxEntryMode`** (`src/StageFright.Core/Enums/`) — `Inclusive` (0, default) \| `Exclusive` (1).
  Persisted on `Settings.TaxEntryMode` as its member name string.
* **`TaxCalculator`** (`src/StageFright.Core/Modules/Finance/`) gains, alongside `SplitInclusive`:
  * `SplitExclusive(decimal net, decimal ratePercent, int minorUnitDigits = 2)` → `(decimal Gross,
    decimal Tax)` with `tax = round(net × ratePercent ÷ 100, minorUnitDigits, AwayFromZero)`,
    `gross = net + tax`.
  * `Split(decimal enteredAmount, TaxEntryMode mode, decimal ratePercent, int minorUnitDigits = 2)`
    → `(decimal Gross, decimal Net, decimal Tax)` — the single dispatch point every taxable posting
    service uses (`Inclusive` → `SplitInclusive`, `Exclusive` → `SplitExclusive`); `gross` always
    equals `net + tax`.
* **`FeeService`, `IncomeEntryService`, `ExpensePaymentService`, `AttendanceService`** call
  `TaxCalculator.Split(entered, settings.TaxEntryMode, rate, digits)`. In `Exclusive` mode the entered
  figure is the net: the primary receivable/bank line, `Fee.Amount`, `Payment.Amount` and (attendance)
  both legs of the paid-at-creation cash pair carry the gross; the income/expense line keeps the net;
  the tax clearing line is unchanged. `Inclusive` mode is byte-identical to pre-#354. The GL line
  structure, the `2310` / `2320` accounts and the `TaxCode` values are untouched (FR-031–FR-033).
* **`ReactivationForgivenessService`** is **not** changed — it reverses a *stored* gross `Fee.Amount`,
  and `SplitInclusive` of an exact gross re-sums to it by construction, so the write-off stays
  balanced whichever entry mode raised the fee and no historical figure is reinterpreted.
* **Setup / Settings plumbing**: `SetupFormModel` `+ TaxEntryMode TaxEntryMode = Inclusive`;
  `SetupRequest` `+ TaxEntryMode TaxEntryMode = Inclusive` (trailing); `SetupService.InitializeAsync`
  persists `request.IsTaxApplicable ? request.TaxEntryMode : Inclusive`; `SalesTaxTab` and
  `TaxSettingsTab` render an Inclusive/Exclusive `<InputSelect>` while tax applies (and reset to
  `Inclusive` on toggle-off); `SettingsService.SaveAsync` forces `Inclusive` when `!IsTaxApplicable`;
  `GeneralSettingsTab.HandleSaveAsync` merges `TaxEntryMode` from the fresh fetch like the other
  tax-owned fields.
* **UI hints**: `RecordIncome` / `ExpensePaymentPage` load `_taxEntryMode` and pick
  `Finance_Common_TaxExclusiveHint` (`Plus tax of {Amount} — total {Total}`) vs the existing
  `Finance_Common_TaxInclusiveHint`, and the Amount field label reflects the mode
  (`Finance_Common_AmountLabelTax{Inclusive,Exclusive}`).
* **Resources**: `Finance_Common_TaxExclusiveHint`, `Finance_Common_AmountLabelTaxInclusive`,
  `Finance_Common_AmountLabelTaxExclusive`; `Settings_Tax_EntryModeLabel`, `Setup_Tax_EntryModeLabel`;
  `Enum_TaxEntryMode_Inclusive` / `Enum_TaxEntryMode_Exclusive` — neutral + `.en-US` + `.fr-FR`,
  `qps-ploc` regenerated; `Us2LocalizationGuardTests.UserFacingEnums` gains `typeof(TaxEntryMode)`.

---

## 13. Phase 16 — recoverable input tax as a balance-sheet asset (issue #355)

* **Seed change** (`StageFrightDbContext.SeedSystemAccounts`) — the system account with `Id`
  `00000000-0000-0000-0000-000000000005` changes from `Name = "Tax Paid"`, `Type = Liability` to
  `Name = "Tax Receivable"`, `Type = Asset`. `AccountNumber` stays `"2320"` and `SortOrder` stays
  `11`. Tax paid on purchases is recoverable from the tax authority, so it is an asset (a receivable);
  `2310` "Tax Collected" (owed to the authority) stays a `Liability`.
* **Migration** `ReclassifyInputTaxAsReceivable` — `UpdateData` on the two columns above; `Down`
  restores `"Tax Paid"` / `"Liability"`. The paired `.Designer.cs` and `StageFrightDbContextModelSnapshot`
  reflect the new seed values. No column added or dropped.
* **Number kept in the 2000s.** `AccountNumber` immutability, and the fact that `Transaction.GLAccount`
  is a denormalised posting-time string snapshot on every historical ledger row, make renumbering
  `2320` into the asset range a data-rewrite with an audit cost out of proportion to a presentation
  fix. `GetNextAccountNumberAsync` is unaffected — it already excludes system accounts from its
  max-in-range scan and `2320` is outside the `1000–1999` asset window it searches.
* **Reports.** `BalanceSheetReportProvider` and `TrialBalanceReportProvider` section purely by
  `AccountType`, so `2320` now falls under Assets with a debit-normal (positive) balance and no
  provider change. `TaxSummaryReportProvider` is unchanged: `taxOnPurchases` and `net` are computed
  from directional GL movements (`GetAccountMovementsAsync`), not the account's classification, so the
  sign convention (`net = tax on sales − tax on purchases`) needs no flip.
* **`OpeningBalanceService`** is unchanged in code — `ToNormalSide` already keys off `account.Type`,
  so a positive carried-over balance for `2320` now posts debit-normal (asset) instead of
  credit-normal. `SystemAccounts` / `AccountNumberAssignmentService` / `Account` doc-comments are
  refreshed to "Tax Receivable"; the `SystemAccounts.TaxPaid*` C# identifiers are retained for
  continuity.
* **No persisted-state change beyond the one seed row.** No `Settings` field, no `TaxCode`, no ledger
  line, no stored monetary amount. An `AUD` dataset is byte-identical (SC-016, FR-031–FR-033;
  `ReclassifyInputTaxAsReceivableMigrationTests`, `RecoverableInputTaxClassificationTests`,
  `AudZeroDriftTests`).
