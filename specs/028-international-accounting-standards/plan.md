# Implementation Plan: International accounting-practice readiness

**Branch**: `028-international-accounting-standards` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/028-international-accounting-standards/spec.md`

## Summary

The finance module is a correct double-entry system built for a single Australian organisation: the
currency symbol and code are hard-coded, two money-entry forms parse amounts with the wrong culture,
the financial year has no start-day concept, and several statements lack the integrity checks and
disclosures an outside reviewer expects. This feature makes the accounting practices safe and portable
for community groups outside Australia **without changing any stored amount or the ledger engine** —
every change is presentation, input validation, or new configuration.

The technical approach reuses the seams spec 027 (localization) already established. Currency becomes a
new fixed-after-setup `Settings.CurrencyCode` (ISO 4217, default `AUD`); `MoneyFormatter` is converted
from a hard-coded-`$` static helper into a process-wide-configured one (set once at startup exactly as
the display culture already is), backed by a new internal `CurrencyCatalog` — no new NuGet dependency.
The money-entry bug is fixed by parsing the always-invariant value of an `<input type="number">` with
`CultureInfo.InvariantCulture` through one shared helper. Statement integrity, the basis-of-accounting
disclosure, and the conventional bank-reconciliation layout are changes inside `StageFright.Reports`
providers plus one new optional `ReportData` field. Period locking is enforced at the single GL choke
point (`GLRepository.AddBalancedSetAsync` / `AddPairAsync`) via a new `ClosedPeriodException`. The
financial-year start gains a day component; audit retention gains a higher default and a surfaced purge
failure; and two documents (an accounting-policy reference and a sales-tax internationalisation
assessment) are published while the finance living spec is de-drafted.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (MAUI Blazor Hybrid)

**Primary Dependencies**: no new packages. Existing: `Microsoft.Extensions.Localization`,
EF Core 10 (SQLite), QuestPDF, CsvHelper, Radzen.Blazor, Blazor.Bootstrap. ISO 4217 currency
metadata (symbol, minor-unit digits, display name) ships as an internal curated catalog, mirroring
`SupportedLanguagesCatalog` from spec 027.

**Storage**: single shared SQLite database via `StageFrightDbContext`. One new migration adds three
`Settings` columns and changes one column default; no data rows are rewritten.

**Testing**: xUnit v3 (unit + integration against file/in-memory SQLite), bUnit (component), the
`StageFright.Localization.Tests` literal-guard, plus a new before/after regression check that an
existing `AUD` reference dataset produces byte-identical report figures and stored values.

**Target Platform**: Windows and macOS desktop (Blazor Hybrid).

**Project Type**: desktop app — layered solution with module slices in `StageFright.Core` (see
Constitution §4.1).

**Performance Goals**: unchanged. Reports stay synchronous; currency formatting is O(1) per cell.

**Constraints**: FR-031 — no previously stored monetary amount, tax amount, or GL balance may change.
FR-032 — balanced atomic postings, GL as the single source of truth, immutable `Fee`/`Payment`/
`Transaction`/`JournalEntry`, corrections only by reversing entries — all preserved. FR-033 — no
change to tax posting mechanics or stored tax code values. Single currency per organisation; no
multi-currency, no FX, no in-place currency change.

**Scale/Scope**: ~45 files across `StageFright.Core`, `StageFright.Data`, `StageFright.Reports`,
`StageFright.UI`, `StageFright.App`, plus 2 docs and the finance living spec. 10 user stories,
33 functional requirements.

## Constitution Check

*GATE: passed before Phase 0. Re-checked after Phase 1 design — still passing.*

| Principle | Assessment |
|-----------|------------|
| §3.2.1 One class per file | PASS — each new type (`SupportedCurrency`, `CurrencyCatalog`, `ClosedPeriodException`, `IClosedPeriodGuard`, `ClosedPeriodGuard`, `MoneyInput`) gets its own file named after it. |
| §3.3 Separation of concerns | PASS with note — currency formatting and money-input parsing live in `StageFright.Core/Localization`; the period-lock guard reads `Settings` from inside `GLRepository`. That is a data-access class consulting configuration, but it already enforces the GL-balance business invariant pre-persistence, so a paired `IClosedPeriodGuard` abstraction at the same choke point is consistent, not a new coupling. Recorded in research.md, Decision 4. |
| §3.4 / §3.5 Soft-delete & financial immutability | PASS — no entity is deleted; `Fee`/`Payment`/`Transaction`/`JournalEntry` are untouched (FR-031/FR-032). Only `Settings` (the singleton, soft-delete-exempt already) gains fields. |
| §3.6 Financial corrections via reversals | PASS — unchanged; the feature adds no correction path. |
| §4.1 Layered architecture / module slices | PASS — changes land in `Modules/Finance`, `Modules/Settings`, `Modules/Rehearsals`, `StageFright.Reports/Providers`, `StageFright.UI/Pages`, and the `StageFright.App` composition root. |
| §4.3 Settings system | PASS — the three built-in setup/settings surfaces (currency, financial-year start, close-period) extend existing hardcoded tabs; no `ISettingsTabProvider` change. |
| §5.2 Custom exceptions at boundaries | PASS — a new `sealed ClosedPeriodException : Exception` follows the mandated five-member shape; the UI maps it to a friendly per-form message. |
| §7.1 Technology stack | PASS — no new dependency; ISO 4217 metadata is an internal catalog. |
| §7.3 No custom JavaScript | PASS — all logic is C#/Blazor. |
| §4.7 Blazor component patterns | PASS — new setup controls ship as paired `.razor` + `.razor.cs`. |
| §11 Testing standards | PASS — every FR maps to unit/integration/bUnit coverage plus a zero-drift regression check; enumerated per story in this plan and to be itemised in tasks. |

No unjustified violations — **Complexity Tracking omitted** (the one separation-of-concerns note above
is a justified, precedent-backed placement, not a violation requiring a simpler-alternative table).

## Project Structure

### Documentation (this feature)

```text
specs/028-international-accounting-standards/
├── spec.md                  # existing
├── plan.md                  # this file
├── research.md              # Phase 0 — 10 decisions
├── data-model.md            # Phase 1 — Settings deltas, new types, migration, exception
├── contracts/               # Phase 1
│   ├── currency-formatting-contract.md
│   ├── settings-and-setup-contract.md
│   ├── reports-contract.md
│   └── period-lock-contract.md
└── checklists/              # existing
```

### Source code (repository root)

```text
src/StageFright.Core/
├── Entities/Settings.cs                              # + CurrencyCode, FinancialYearStartDay, ClosedThroughDate; AuditRetentionYears default 1→5
├── Exceptions/ClosedPeriodException.cs               # NEW — §5.2 shape
├── Contracts/
│   ├── IClosedPeriodGuard.cs                         # NEW — checks a posting date against Settings.ClosedThroughDate
│   └── ISettingsRepository.cs / ISetupService.cs     # signature deltas for new Settings fields
├── Localization/
│   ├── MoneyFormatter.cs                             # static → process-wide-configured (Configure + currency-aware Format/FormatWithCode)
│   ├── SupportedCurrency.cs                          # NEW — Code, Symbol, MinorUnitDigits, DisplayName
│   ├── CurrencyCatalog.cs                            # NEW — curated ISO 4217 list + lookup, mirrors SupportedLanguagesCatalog
│   └── MoneyInput.cs                                 # NEW — invariant parse for <input type=number> money values
├── Modules/Finance/
│   ├── FinancialYearCalculator.cs                    # GetRange/GetPreviousRange gain a startDay parameter
│   ├── TaxCalculator.cs                              # round to configured minor-unit digits, not hard-coded 2
│   └── ClosedPeriodGuard.cs                          # NEW — IClosedPeriodGuard implementation
├── Modules/Settings/
│   ├── SetupRequest.cs / SetupService.cs            # + CurrencyCode, FinancialYearStartDay
│   └── SettingsService.cs                            # reject a CurrencyCode change after it is set
└── Modules/Rehearsals/AttendanceService.cs          # write an AuditTrailEntry for the fee accrual + auto-payment (FR-026)

src/StageFright.Data/
├── Configurations/SettingsConfiguration.cs          # map 3 new columns; AuditRetentionYears default 5
├── Migrations/<ts>_AddInternationalAccountingSettings.cs   # NEW — add columns, backfill AUD + startDay 1, no row rewrite
└── Repositories/GLRepository.cs                      # AddBalancedSetAsync / AddPairAsync consult IClosedPeriodGuard before SaveChangesAsync

src/StageFright.Reports/
├── Models/ReportData.cs                              # + BasisOfAccounting (string?, optional)
├── Rendering/PdfReportRenderer.cs                    # render BasisOfAccounting
├── Rendering/CsvReportExporter.cs                    # append BasisOfAccounting as trailing note row(s)
├── Resources/ReportsResource.resx (+ .en-US/.fr-FR)  # new keys: basis line, out-of-balance, adjusted-balance rec labels
└── Providers/
    ├── TrialBalanceReportProvider.cs                 # tolerance 0.01 → exact; money via MoneyFormatter
    ├── BalanceSheetReportProvider.cs                 # explicit out-of-balance line; money via MoneyFormatter
    ├── BankReconciliationReportProvider.cs           # rewrite to statement-balance ± outstanding = ledger-balance
    ├── IncomeStatementReportProvider.cs              # basis line; startDay; money via MoneyFormatter
    ├── TaxSummaryReportProvider.cs                   # basis line; startDay; money via MoneyFormatter
    ├── AccountRegisterReportProvider.cs              # money via MoneyFormatter
    └── GeneralLedgerReportProvider.cs                # money via MoneyFormatter

src/StageFright.UI/
├── Pages/Setup/SetupFormModel.cs                     # + CurrencyCode, FinancialYearStartMonth, FinancialYearStartDay; AuditRetentionYears default 5
├── Pages/Setup/Tabs/GeneralAppearanceTab.razor(.cs) # currency picker + financial-year-start month/day pickers
├── Pages/Finance/JournalEntryPage.razor.cs          # ParseAmount → MoneyInput.Parse (invariant); ClosedPeriodException mapping
├── Shared/OpeningBalanceEntryForm.razor.cs          # SetAmount → MoneyInput.Parse (invariant)
├── Pages/Finance/*.razor(.cs) + Modules/Finance/*Tile* # money already via MoneyFormatter — behaviour follows configured currency automatically
├── Pages/Settings/GeneralSettingsTab.razor(.cs)     # "close periods through <date>" control + confirm; audit-retention default label
└── (finance posting forms)                           # catch ClosedPeriodException → friendly message

src/StageFright.App/MauiProgram.cs                    # after culture resolve: MoneyFormatter.Configure(currency); register IClosedPeriodGuard; surface a failed audit purge

docs/
├── accounting-policies.md                            # NEW — FR-027
└── assessments/sales-tax-internationalisation.md     # NEW — FR-029/FR-030

capabilities/finance/spec.md                          # FR-028 — de-[DRAFT], drop retired GST/ABN/BAS wording, reflect TaxCode/TaxRate model

tests/  (matching projects)                           # unit + integration + bUnit per FR; AUD zero-drift regression
```

**Structure Decision**: No new project or structural pattern. The feature is delivered as targeted
edits within the existing six-project layered solution, concentrated in `StageFright.Core/Localization`
(currency + input), `StageFright.Reports/Providers` (statements), `StageFright.Data` (one migration +
the GL choke-point guard), and the `StageFright.UI` setup/settings surfaces — following the same seams
spec 027 used for display language and culture.

## Approach by user story

Priority order (P1 → P3) matches the spec. Each story is independently testable and shippable.

### US1 — Configurable currency (P1, blocker) · FR-001…006, FR-031

* `Settings.CurrencyCode` (`string`, ISO 4217, non-null, default `"AUD"`). Set at first-run setup,
  never editable afterward (`SettingsService.SaveAsync` rejects a change; no currency control on any
  Settings edit tab).
* New `SupportedCurrency` (Code, Symbol, MinorUnitDigits ∈ {0,2,3}, DisplayName) + `CurrencyCatalog`
  (curated list covering the shipped/common set — `AUD`, `USD`, `EUR`, `GBP`, `NZD`, `CAD`, `JPY`
  (0-decimal), `KWD`/`BHD` (3-decimal) as representatives — with a lookup and an `All` enumeration
  the setup picker binds to; extensible by adding a row, no code change elsewhere).
* `MoneyFormatter` keeps its `Format` / `FormatWithCode` call surface (≈20 call sites unchanged) but
  gains `Configure(SupportedCurrency)`, called once in `MauiProgram` right after the display culture
  is resolved. It then formats with the configured symbol/code and the configured minor-unit digits,
  while grouping and symbol placement continue to follow `CultureInfo.CurrentCulture` (FR-003). It
  also pins `NumberFormatInfo.CurrencyNegativePattern` to the leading/trailing-minus form matching the
  culture's symbol placement (never accounting parentheses), so a negative AUD figure is `-$42.10` on
  every host — the invariant culture that CI hosts default to would otherwise emit `($42.10)` and
  break the FR-006 / SC-004 zero-drift regression.
* `StageFright.Reports` providers currently emit bare `ToString("F2")` money strings with no symbol.
  Each provider's private `FormatCurrency` is redirected through `MoneyFormatter` so printed/exported
  amounts carry the configured symbol and precision and never show a mismatched one (FR-003/FR-004).
* `TaxCalculator.SplitInclusive` rounds to `CurrencyCatalog` minor-unit digits instead of a literal
  `2`; the `net = gross − tax` remainder keeps the parts re-summing exactly (FR-005).
* Migration backfills the single existing `Settings` row to `CurrencyCode = 'AUD'`; an existing AUD
  dataset shows `$`, two decimals, and identical stored values (FR-006).
* Setup: a required currency `<select>` (default `AUD`) in `GeneralAppearanceTab`; `SetupFormModel`
  → `SetupRequest` → `SetupService` carry `CurrencyCode`.

### US2 — Locale-safe money entry (P1, live bug) · FR-007…009

* `JournalEntryPage.ParseAmount` and `OpeningBalanceEntryForm.SetAmount` both call
  `decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, …)` on the `.value` of an
  `<input type="number">`, which the browser always serialises invariant (`"1.5"`). Under `fr-FR` /
  `de-DE` the `.` is read as a group separator and `1.50` posts as `150`.
* New shared `MoneyInput.Parse(string?) : decimal` parses with `CultureInfo.InvariantCulture` +
  `NumberStyles.AllowDecimalPoint | AllowLeadingSign` (matching how Blazor's own `@bind` binds a
  numeric input to `decimal`). Both hand-rolled parsers switch to it; a repo-wide guard confirms no
  other money field parses a numeric-input value with `CurrentCulture` (FR-008).
* Inputs stay `type="number"` — the browser supplies the locale-appropriate numeric keypad and
  digit shaping, so the "local representation of one and a half" is served without a period-as-grouping
  ambiguity (FR-009). Rationale in research.md, Decision 3.

### US3 — Statement integrity (P2) · FR-010, FR-011

* `TrialBalanceReportProvider`: `Math.Abs(totalDebits − totalCredits) > 0.01m` becomes `!= 0m`; the
  thrown `GLBalanceException` message and the doc-comment (stale "FR-034" reference) are updated. The
  viewer already catches it and offers "Try Again" (FR-011, no tolerance band).
* `BalanceSheetReportProvider`: after building the three sections, assert
  `totalAssets == totalLiabilities + totalEquity`. On any non-zero difference, append a bold explicit
  "Out of balance by <amount>" row (a new `ReportsResource` key) so a clean statement is never
  produced (FR-010). The Balance Sheet balances by construction today, so a non-zero here signals a
  real integrity fault and the visible figure is the diagnostic. Rationale in research.md, Decision 5.

### US4 — Basis-of-accounting disclosure (P2) · FR-012

* New optional `ReportData.BasisOfAccounting` (`string?`). `PdfReportRenderer` renders it under the
  subtitle/generation line; `CsvReportExporter` appends it as a trailing labelled note row;
  `ReportViewer.razor` shows it beneath the subtitle.
* The financial-statement providers — Income Statement, Balance Sheet, Trial Balance, Tax Summary,
  Account Register, General Ledger, Bank Reconciliation, Member Account Summary — set it from one
  shared localized string that describes the hybrid basis accurately: member fees recognised when
  levied (accrual); all other income and expenditure recognised when received or paid (cash).
  Member List and Committee reports leave it null (not financial statements).

### US5 — Conventional bank reconciliation (P2) · FR-013…015

* `BankReconciliationReportProvider.BuildAccountSectionsAsync` is rewritten to the standard
  adjusted-balance form per account: **balance per bank statement** → **add** outstanding deposits
  (each listed and summed) → **less** outstanding payments (each listed and summed) → **adjusted bank
  balance** → **balance per general ledger** as at the statement date → a line demonstrating the two
  are equal.
* Outstanding items come from the existing `IGLRepository.GetUnreconciledByAccountAsync`; the ledger
  balance from the existing `IGLRepository.GetAccountBalanceAsync(accountId, statementDate)`. The
  outstanding totals are carried into the adjusted-balance arithmetic, not merely listed (FR-014).
* New `ReportsResource` keys for the conventional labels; the four-column
  Date/Description/Deposit/Payment shape is retained where it fits, with the summary block using the
  label/amount rows.
* FR-015 (finalisation still requires balancing; a finalised reconciliation stays immutable) is
  already enforced by `BankReconciliationService` — covered by added tests, no behaviour change.

### US6 — Closed-period lock (P3) · FR-016…018

* `Settings.ClosedThroughDate` (`DateTime?`, null = nothing closed).
* New `sealed ClosedPeriodException : Exception` (§5.2 shape). New `IClosedPeriodGuard` +
  `ClosedPeriodGuard` (reads the singleton `Settings`) with `EnsureOpen(DateTime postingDate)`.
* `GLRepository.AddBalancedSetAsync` / `AddPairAsync` call the guard for every line's `Date` before
  `SaveChangesAsync`. Because this is inside `UnitOfWork.ExecuteInTransactionAsync`, a rejection rolls
  the whole operation back — no business row and no ledger line persists (FR-017). This is the single
  choke point every posting path funnels through.
* FR-018: opening balances entered during first-run setup are always accepted because setup completes
  before any period can be closed (`ClosedThroughDate` is null then). No wizard carve-out is needed;
  the reasoning is documented. (If a bypass is ever required, the ambient-scope pattern from
  `AuditTrailSuppressionScope` is the template.)
* UI: a "close all periods through <date>" date control + explicit confirmation on the General
  settings tab (FR-016); every finance posting form catches `ClosedPeriodException` and shows a
  friendly message.

### US7 — Financial-year start as a setup decision (P3) · FR-019…022

* `Settings.FinancialYearStartDay` (`int`, 1–28, default 1). `FinancialYearStartMonth` keeps its name
  and default 7 (Verbatim Constraint).
* `FinancialYearCalculator.GetRange` / `GetPreviousRange` gain a `startDay` parameter; the year pivot
  compares `(month, day)` and the range runs `start … start.AddYears(1).AddDays(-1)`. Callers —
  `TrialBalance`, `BalanceSheet`, `IncomeStatement`, `TaxSummary` providers and
  `OpeningBalancesWizard` — pass `settings.FinancialYearStartDay`.
* Setup: month + day pickers in `GeneralAppearanceTab`, always visible and mandatory (defaults
  month 7 / day 1), so there is no silent Australian default (FR-019). Non-first-of-month starts are
  supported (FR-020); all FY-preset reports and dashboard figures honour month **and** day (FR-021).
* Existing AU dataset (month 7, day 1) is unchanged — the migration backfills `FinancialYearStartDay
  = 1` (US7 AC-3 / SC-004).
* FR-022 (sub-twelve-month first year / part-year label) is **out of scope for this feature** and
  captured as a follow-on issue (spec Assumptions permit this). Rationale in research.md, Decision 9.

### US8 — Audit retention, purge hardening, coverage (P3) · FR-023…026

* `Settings.AuditRetentionYears` default `1 → 5`, everywhere it is declared (`Settings.cs`,
  `SettingsConfiguration`, `SetupFormModel`, `SetupRequest`, `SetupService`). Range stays 1–7 and the
  field stays user-configurable (FR-023, spec Assumptions).
* The migration changes only the column default — it issues no `UpdateData` — so an existing row
  keeps its configured value (FR-024).
* `MauiProgram` already logs a failed startup purge; it additionally records the failure into the
  existing retrievable startup-diagnostic state so the UI can surface it (FR-025). Rationale in
  research.md, Decision 8.
* `AttendanceService` posts an attendance-fee accrual (and, when paid at creation, an auto-payment)
  inside its transaction but writes no `AuditTrailEntry` for them — add `IAuditTrailService.LogAsync`
  calls for both, inside the same transaction (FR-026).

### US9 — Documentation (P3) · FR-027, FR-028

* `docs/accounting-policies.md` (new): basis of accounting, revenue recognition, rounding, currency,
  record immutability and the reversing-entry correction method, audit-trail retention, and an
  explicit statement that the reports are unaudited management accounts (FR-027).
* `capabilities/finance/spec.md`: remove the `> [DRAFT]` marker and rewrite the tax-related
  requirements/scenarios from the retired registration-based GST/ABN/BAS model (`IsGstRegistered`,
  `gross ÷ 11`, "GST-registered") to the current `Settings.IsTaxApplicable` / `TaxRate` / `TaxCode`
  model from spec 016 (FR-028). The `capabilities/audit-trail/spec.md` retention figure is corrected
  in this feature — raising the `AuditRetentionYears` default from 1 to 5 makes it stale directly
  (project Spec & Docs Workflow rule). Stale GST/ABN wording in `capabilities/settings/spec.md`
  predates this feature and is noted as a related follow-up (FR-028 names only the finance spec).
* Stale prose in `CLAUDE.md` (Localization section: "keeps `$` / `AUD` fixed"), the `MoneyFormatter`
  doc-comment ("ALWAYS Australian dollars"), and spec 027's docs are corrected in the same task
  (project Spec & Docs Workflow rule).

### US10 — Sales-tax internationalisation assessment (P3, spike) · FR-029, FR-030, FR-033

* `docs/assessments/sales-tax-internationalisation.md` (new): for each of rate changes over time,
  tax-exclusive entry, the balance-sheet classification of recoverable tax (accounts `2310` /
  `2320`), and whether multiple simultaneous rates or jurisdictions are needed — an in-scope /
  out-of-scope decision with a rough size, and a follow-on GitHub issue for every in-scope point
  (FR-029/FR-030).
* No tax posting mechanic and no stored tax code value changes in this feature (FR-033 / US10 AC-3).

### Cross-cutting guards · FR-031, FR-032, FR-033

A regression test fixture loads a known `AUD` reference dataset and asserts that every financial
report's figures and every stored monetary/tax/GL value are identical before and after the change
(SC-004). The double-entry guarantees in `GLRepository` and `UnitOfWork` are unchanged and re-covered.

## Testing strategy (plan level)

* **Unit** — `CurrencyCatalog` lookups incl. 0- and 3-decimal currencies; `MoneyFormatter` under
  `en-AU`, `en-US`, `fr-FR`, `de-DE`, and a yen configuration; `MoneyInput.Parse` for `1.5`, `1.50`,
  `1000.5`, blank, and garbage under `fr-FR`/`de-DE`; `FinancialYearCalculator` for non-first-of-month
  and Feb/short-month start days; `TaxCalculator` re-sum exactness at 0/2/3 minor digits;
  `ClosedPeriodGuard` boundary (date exactly on the closed-through date is rejected).
* **Integration** — a posting dated into a closed period leaves no `Fee`/`Payment`/`Transaction`
  row; setup with a non-AUD, non-first-of-month configuration flows end to end; the audit-retention
  default on a fresh dataset is 5; an existing dataset's configured retention survives the migration;
  attendance that accrues a fee writes an audit entry; the AUD zero-drift regression.
* **bUnit / report** — Balance Sheet from an unbalanced ledger renders the out-of-balance line and
  never a clean statement; Trial Balance off by one cent fails; every financial statement carries the
  basis line; the bank reconciliation shows both balances and proves equality with and without
  outstanding items.
* **Acceptance** — one per P1/P2 story mapped to its spec scenarios (Constitution §11.4).
