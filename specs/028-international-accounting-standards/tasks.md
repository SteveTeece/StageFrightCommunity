# Tasks: International accounting-practice readiness

**Feature**: `028-international-accounting-standards` · **Size**: oversized · **Spec**: [spec.md](spec.md) ·
**Plan**: [plan.md](plan.md)

Line format: `- [ ] **T###** [P?] [US#] Description · exact/file/path`.
`[P]` = independent of the other tasks in its wave (different file, no incomplete dependency).
`[US#]` = the user story the task serves. Waves are separated by explicit join lines.
A letter-suffixed id (e.g. `T014a`) is an acceptance or guard task added after initial numbering; it
sorts with its numeric sibling and does not renumber the sequence.

Test-first: every `### Tests` block is written to **fail first**, then made green by that phase's
`### Implementation` waves (Constitution §11).

---

## Phase 1: Setup

- [X] **T001** Confirm a green baseline before any change — `dotnet build` and `dotnet test` (full run, no `--no-build`); record counts · `StageFrightCommunity.slnx` — baseline: build 0W/0E; 1805 tests pass (Core 620, Reports 217, UI 599, Data 154, Localization 23, Integration 192)

---

## Phase 2: Foundational (BLOCKS US1, and the report-money work in US3–US5, US7)

Shared currency primitives, the `Settings` schema for all four new/changed fields, and the one
migration. No user-story phase starts until this phase is done.

**Wave 1 — independent (different files):**

- [X] **T002** [P] `SupportedCurrency` record — `Code`, `Symbol`, `MinorUnitDigits` (0/2/3), `DisplayName` · `src/StageFright.Core/Localization/SupportedCurrency.cs`
- [X] **T003** [P] `Settings` entity — add `CurrencyCode` (`string`, default `"AUD"`), `FinancialYearStartDay` (`int`, default `1`), `ClosedThroughDate` (`DateTime?`, default `null`); change `AuditRetentionYears` default `1 → 5` (range 1–7 unchanged) · `src/StageFright.Core/Entities/Settings.cs`

**⟶ Wait for Wave 1 to finish, then:**

- [X] **T004** [P] `CurrencyCatalog` static — `All` seed set (`AUD`,`USD`,`EUR`,`GBP`,`NZD`,`CAD`,`JPY` 0-digit, `KWD`/`BHD` 3-digit); `bool TryGet(string, out SupportedCurrency)` case-insensitive, returns `false` + `Default` on a miss (no throw); `SupportedCurrency Get(string)` case-insensitive, throws `ValidationException` on an unknown code; `Default` (`AUD`/`$`/2) · `src/StageFright.Core/Localization/CurrencyCatalog.cs` *(needs T002)*
- [X] **T005** [P] `SettingsConfiguration` — map the 3 new columns; `AuditRetentionYears` `HasDefaultValue(5)` · `src/StageFright.Data/Configurations/SettingsConfiguration.cs` *(needs T003)*

**⟶ then:**

- [X] **T006** EF migration `AddInternationalAccountingSettings` — `CurrencyCode` `TEXT NOT NULL DEFAULT 'AUD'`, `FinancialYearStartDay` `INTEGER NOT NULL DEFAULT 1`, `ClosedThroughDate` `TEXT NULL`, `AuditRetentionYears` default constraint `→ 5` **only** (no `migrationBuilder.UpdateData`) · `src/StageFright.Data/Migrations/20260829064637_AddInternationalAccountingSettings.cs` *(needs T005)*
- [X] **T007** `MoneyFormatter` — add `Configure(SupportedCurrency)`; `Format`/`FormatWithCode` use the configured symbol/code and `MinorUnitDigits`, grouping/placement still from `CultureInfo.CurrentCulture`; before `Configure`, fall back to `CurrencyCatalog.Default` · `src/StageFright.Core/Localization/MoneyFormatter.cs` *(needs T004)*

**⟶ then:**

- [X] **T008** `MauiProgram` — after the display culture is resolved and applied, call `MoneyFormatter.Configure(CurrencyCatalog.Get(settings?.CurrencyCode ?? "AUD"))` · `src/StageFright.App/MauiProgram.cs` *(needs T007)*

**Checkpoint**: currency catalog + configurable formatter + `Settings` schema exist; an `AUD` dataset
formats byte-identically (`$`, 2 decimals) — T001 tests still green.

---

## Phase 3: User Story 1 — Run the books in the organisation's own currency (P1)

**Goal**: every amount on every screen, report, PDF and CSV shows the organisation's configured
currency with the right symbol and minor-unit precision; nothing asserts `$`/`AUD` for a non-AUD org;
an existing `AUD` dataset is unchanged.

**Independent Test**: complete setup with a non-Australian currency (incl. a zero-decimal one such as
`JPY`), record a fee and a payment, generate every financial report — every amount uses the chosen
currency and precision and no `$`/`AUD` appears.

### Tests

**Wave 1 — independent (different files):**

- [x] **T009** [P] [US1] Unit — `CurrencyCatalog` lookups incl. 0- and 3-decimal; `Get` throws `ValidationException` on an unknown code; `TryGet` returns `false` + `Default` on a miss; both case-insensitive · `tests/StageFright.Core.Tests/Localization/CurrencyCatalogTests.cs`
- [x] **T010** [P] [US1] Unit — `MoneyFormatter` under `en-AU`/`en-US`/`fr-FR`/`de-DE` and a `JPY` config: symbol, digit count, grouping · `tests/StageFright.Core.Tests/Localization/MoneyFormatterTests.cs`
- [x] **T011** [P] [US1] Unit — `TaxCalculator.SplitInclusive` parts re-sum to gross exactly at 0/2/3 minor digits · `tests/StageFright.Core.Tests/Modules/Finance/TaxCalculatorTests.cs`
- [x] **T012** [P] [US1] Integration — setup with a non-`AUD` currency persists `CurrencyCode`; `SettingsService.SaveAsync` rejects a later `CurrencyCode` change · `tests/StageFright.Core.Tests/Modules/Settings/SetupServiceTests.cs`, `SettingsServiceTests.cs`
- [x] **T013** [P] [US1] Integration — **AUD zero-drift**: a reference dataset produces identical report figures and identical stored values before/after (SC-004, FR-006, FR-031) · `tests/StageFright.Integration.Tests/InternationalAccounting/AudZeroDriftTests.cs`
- [x] **T014** [P] [US1] bUnit — setup currency picker renders `CurrencyCatalog.All`, defaults `AUD`, binds the model · `tests/StageFright.UI.Tests/Pages/Setup/CurrencyPickerTests.cs`
- [x] **T014a** [P] [US1] **Acceptance** — `V28_CurrencyConfiguration` drives US1 AC-1…AC-4 end to end: fresh install → setup picks a non-`AUD` currency (incl. a zero-decimal `JPY`) → record a fee + payment → generate every financial report; asserts the chosen symbol and minor-unit precision throughout, no `$`/`AUD`, regional grouping/placement with the configured symbol unchanged (AC-3), zero-decimal reconciles exactly (AC-2), and an `AUD` dataset is byte-identical (AC-4) · `tests/StageFright.Integration.Tests/Scenarios/V28_CurrencyConfigurationTests.cs`

### Implementation

**Wave 1 — rounding (single file, then its callers):**

- [x] **T015** [US1] `TaxCalculator.SplitInclusive` — add an optional `int minorUnitDigits = 2` parameter used for rounding (the default keeps every un-updated call site at today's 2-digit behaviour); `net = gross − tax` remains the remainder · `src/StageFright.Core/Modules/Finance/TaxCalculator.cs`
- [x] **T016** [US1] Update every `SplitInclusive` caller to pass the configured currency's `MinorUnitDigits` (`CurrencyCatalog.Get(settings.CurrencyCode).MinorUnitDigits`) — Core services `AttendanceService.cs`, `FeeService.cs`, `IncomeEntryService.cs`, `ExpensePaymentService.cs`, `ReactivationForgivenessService.cs`; UI tax-hint sites `src/StageFright.UI/Pages/Finance/ExpensePaymentPage.razor.cs`, `src/StageFright.UI/Pages/Finance/RecordIncome.razor.cs`; and `src/StageFright.App/Seeding/DebugDataSeeder.cs` *(needs T015)*

**⟶ then — setup plumbing (independent files):**

- [x] **T017** [P] [US1] `SetupFormModel` — add `string CurrencyCode` (`[Required]`, default `"AUD"`) · `src/StageFright.UI/Pages/Setup/SetupFormModel.cs`
- [x] **T018** [P] [US1] `SetupRequest` — add trailing `string CurrencyCode = "AUD"` · `src/StageFright.Core/Modules/Settings/SetupRequest.cs`

**⟶ then:**

- [x] **T019** [US1] `SetupService.InitializeAsync` — validate `CurrencyCode ∈ CurrencyCatalog.All` (`Validation_Setup_CurrencyUnknown`); persist to `Settings.CurrencyCode` · `src/StageFright.Core/Modules/Settings/SetupService.cs` *(needs T018)*
- [x] **T020** [US1] `SettingsService.SaveAsync` — reject a `CurrencyCode` that differs from the persisted value (`Validation_Settings_CurrencyImmutable`) · `src/StageFright.Core/Modules/Settings/SettingsService.cs`
- [x] **T021** [US1] `GeneralAppearanceTab` — add currency `<select id="setup-currency">` bound to `SetupFormModel.CurrencyCode`, options from `CurrencyCatalog.All` · `src/StageFright.UI/Pages/Setup/Tabs/GeneralAppearanceTab.razor` + `.razor.cs` *(needs T017)*

**⟶ then — report money routing (independent providers):**

- [x] **T022** [P] [US1] `IncomeStatementReportProvider` — private `FormatCurrency` → `MoneyFormatter.Format` · `src/StageFright.Reports/Providers/IncomeStatementReportProvider.cs`
- [x] **T023** [P] [US1] `BalanceSheetReportProvider` — `FormatCurrency` → `MoneyFormatter.Format` · `src/StageFright.Reports/Providers/BalanceSheetReportProvider.cs`
- [x] **T024** [P] [US1] `TrialBalanceReportProvider` — `FormatCurrency` → `MoneyFormatter.Format` · `src/StageFright.Reports/Providers/TrialBalanceReportProvider.cs`
- [x] **T025** [P] [US1] `TaxSummaryReportProvider` — money cells → `MoneyFormatter` · `src/StageFright.Reports/Providers/TaxSummaryReportProvider.cs`
- [x] **T026** [P] [US1] Remaining providers — money cells → `MoneyFormatter`: `AccountRegisterReportProvider`, `GeneralLedgerReportProvider`, `ChartOfAccountsReportProvider`, `MemberAccountSummaryReportProvider`, `CommitteeReportProvider`, `MemberListReportProvider` · `src/StageFright.Reports/Providers/*.cs`
- [x] **T027** [P] [US1] `JournalEntryPage` — the debit/credit totals row `ToString("N2")` → `MoneyFormatter.Format` (precision follows currency) · `src/StageFright.UI/Pages/Finance/JournalEntryPage.razor`

> `BankReconciliationReportProvider` money formatting is folded into its US5 rewrite (T051) — same file.

**⟶ then — docs sync (same task, project Spec & Docs Workflow rule):**

- [x] **T028** [US1] Correct stale "always AUD / `$` fixed" prose — `MoneyFormatter` XML doc, `CLAUDE.md` Localization section, `specs/027-localization-support/` lines that assert a fixed `$`/`AUD` · `src/StageFright.Core/Localization/MoneyFormatter.cs`, `CLAUDE.md`, `specs/027-localization-support/*.md`

**Checkpoint**: a non-`AUD` (incl. `JPY`) organisation completes setup; every screen, report, PDF and
CSV shows the configured symbol + precision; no mismatched symbol/code anywhere; the `AUD`
regression (T013) passes.

---

## Phase 4: User Story 2 — Enter amounts correctly in any regional number format (P1)

**Goal**: the manual journal and opening-balance forms store exactly the amount the user intended,
independent of the device region.

**Independent Test**: with the device region set to French and to German, enter known amounts into
the manual journal and opening-balance forms and assert the stored ledger values are exact.

### Tests

**Wave 1 — independent (different files):**

- [x] **T029** [P] [US2] Unit — `MoneyInput.Parse` for `"1.5"`, `"1.50"`, `"1000.5"`, `""`, `"abc"`, `"-3.2"` under `fr-FR` and `de-DE` → exact / `0m` fallback · `tests/StageFright.Core.Tests/Localization/MoneyInputTests.cs`
- [x] **T030** [P] [US2] bUnit — `JournalEntryPage` under `fr-FR`: entering `1.50` yields `line.Debit == 1.50m` · `tests/StageFright.UI.Tests/Pages/Finance/JournalEntryPageLocaleTests.cs`
- [x] **T031** [P] [US2] bUnit — `OpeningBalanceEntryForm` under `de-DE`: entered amount stored exactly · `tests/StageFright.UI.Tests/Shared/OpeningBalanceEntryFormLocaleTests.cs`
- [x] **T032** [P] [US2] Guard — no money field parses an `<input type="number">` value with `CultureInfo.CurrentCulture` · `tests/StageFright.Localization.Tests/MoneyInputGuardTests.cs`
- [x] **T032a** [P] [US2] **Acceptance** — `V28_LocaleSafeMoneyEntry` drives US2 AC-1…AC-3: device region `fr-FR` then `de-DE`, enter known amounts into the manual journal and the opening-balance form; asserts the stored ledger values are exact to the cent and identical to the same input under `en-AU`, and that no digit is read as a thousands separator · `tests/StageFright.Integration.Tests/Scenarios/V28_LocaleSafeMoneyEntryTests.cs`

### Implementation

**Wave 1 — the helper (single file):**

- [x] **T033** [US2] `MoneyInput` — `static decimal Parse(string?)` using `CultureInfo.InvariantCulture` + `NumberStyles.AllowDecimalPoint | AllowLeadingSign`; `0m` for null/blank/unparseable · `src/StageFright.Core/Localization/MoneyInput.cs`

**⟶ Wait for T033, then (independent call sites):**

- [x] **T034** [P] [US2] `JournalEntryPage.ParseAmount` → `MoneyInput.Parse` · `src/StageFright.UI/Pages/Finance/JournalEntryPage.razor.cs`
- [x] **T035** [P] [US2] `OpeningBalanceEntryForm.SetAmount` → `MoneyInput.Parse` · `src/StageFright.UI/Shared/OpeningBalanceEntryForm.razor.cs`

**Checkpoint**: French/German-locale entry into the manual journal and opening balances stores exact
values; the guard test locks it in.

---

## Phase 5: User Story 3 — Trust that the statements are internally consistent (P2)

**Goal**: the Balance Sheet never presents a clean statement when assets ≠ liabilities + equity, and
the Trial Balance treats any non-zero debit/credit difference as an error.

**Independent Test**: generate the Balance Sheet and Trial Balance from a balanced ledger (both tie)
and from a deliberately corrupted ledger (both refuse or flag the discrepancy).

### Tests

**Wave 1 — independent (different files):**

- [x] **T036** [P] [US3] Report test — Trial Balance with debits/credits differing by `0.01` fails to generate (`GLBalanceException`, no tolerance) · `tests/StageFright.Reports.Tests/Providers/TrialBalanceReportProviderTests.cs`
- [x] **T037** [P] [US3] Report test — Balance Sheet from an unbalanced ledger renders an explicit out-of-balance row and never a clean statement; a balanced ledger renders normally · `tests/StageFright.Reports.Tests/Providers/BalanceSheetReportProviderTests.cs`
- [x] **T037a** [P] [US3] **Acceptance** — `V28_StatementIntegrity` drives US3 AC-1…AC-3: Balance Sheet + Trial Balance from a balanced ledger (both tie, statements produced normally) and from a deliberately corrupted ledger (Balance Sheet shows an explicit out-of-balance line and never a clean statement; Trial Balance fails; a one-cent debit/credit difference still fails — no tolerance) · `tests/StageFright.Integration.Tests/Scenarios/V28_StatementIntegrityTests.cs`

### Implementation

**Wave 1 — independent (different providers):**

- [x] **T038** [P] [US3] `TrialBalanceReportProvider` — `Math.Abs(totalDebits − totalCredits) > 0.01m` → `totalDebits != totalCredits`; reword the thrown message; fix the stale `FR-034` doc-comment reference · `src/StageFright.Reports/Providers/TrialBalanceReportProvider.cs`
- [x] **T039** [P] [US3] `BalanceSheetReportProvider` — after totals, if `totalAssets != totalLiabilities + totalEquity` append a bold `Reports_BalanceSheet_OutOfBalance` row (label + `MoneyFormatter.Format(difference)`) · `src/StageFright.Reports/Providers/BalanceSheetReportProvider.cs`

**⟶ then:**

- [x] **T040** [US3] `ReportsResource` — reword `Reports_TrialBalance_GLImbalanceError` (drop "tolerance"); add `Reports_BalanceSheet_OutOfBalance` · `src/StageFright.Reports/Resources/ReportsResource.resx`, `.en-US.resx`, `.fr-FR.resx`

**Checkpoint**: an unbalanced ledger cannot produce a clean Balance Sheet or Trial Balance (SC-005,
SC-006).

---

## Phase 6: User Story 4 — Know the basis of accounting each statement uses (P2)

**Goal**: every financial statement states its basis of accounting on its face, accurately describing
the hybrid treatment (member fees accrual; other activity cash).

**Independent Test**: generate the Income Statement, Balance Sheet and Tax Summary and confirm each
carries an accurate basis-of-accounting statement (screen, PDF and CSV).

### Tests

- [x] **T041** [P] [US4] Report + component tests — each financial statement carries the basis line in PDF, CSV, and the viewer; Member List / Committee do not · `tests/StageFright.Reports.Tests/BasisOfAccountingTests.cs`, `tests/StageFright.UI.Tests/Shared/ReportViewerBasisTests.cs`
- [x] **T041a** [P] [US4] **Acceptance** — `V28_BasisOfAccountingDisclosure` drives US4 AC-1…AC-2: generate the Income Statement, Balance Sheet and Tax Summary (screen, PDF, CSV); asserts each carries a basis-of-accounting line whose wording names both the accrual treatment of member fees and the cash treatment of other income and expenditure — not a single blanket basis · `tests/StageFright.Integration.Tests/Scenarios/V28_BasisOfAccountingDisclosureTests.cs`

### Implementation

**Wave 1 — the model field (single file):**

- [x] **T042** [US4] `ReportData` — add `string? BasisOfAccounting { get; init; }` · `src/StageFright.Reports/Models/ReportData.cs`

**⟶ Wait for T042, then (independent consumers):**

- [x] **T043** [P] [US4] `PdfReportRenderer` — render `BasisOfAccounting` below the "Generated:" line when non-empty · `src/StageFright.Reports/Rendering/PdfReportRenderer.cs`
- [x] **T044** [P] [US4] `CsvReportExporter` — append `BasisOfAccounting` as a trailing labelled note record when non-null · `src/StageFright.Reports/Rendering/CsvReportExporter.cs`
- [x] **T045** [P] [US4] `ReportViewer` — show `BasisOfAccounting` beneath the subtitle when non-null · `src/StageFright.UI/Shared/ReportViewer.razor` + `.razor.cs`
- [x] **T046** [P] [US4] `ReportsResource` — add `Reports_Common_BasisOfAccounting` (hybrid accrual/cash wording; not a single blanket basis) · `src/StageFright.Reports/Resources/ReportsResource.resx`, `.en-US.resx`, `.fr-FR.resx`

**⟶ then:**

- [x] **T047** [US4] Set `BasisOfAccounting` from `Reports_Common_BasisOfAccounting` in the eight financial-statement providers: Income Statement, Balance Sheet, Trial Balance, Tax Summary, Account Register, General Ledger, Bank Reconciliation, Member Account Summary · `src/StageFright.Reports/Providers/*.cs`

**Checkpoint**: every financial statement states its hybrid basis on its face (SC-007).

---

## Phase 7: User Story 5 — Read a conventional bank reconciliation (P2)

**Goal**: the bank reconciliation report follows the standard adjusted-balance layout — balance per
bank statement, adjusted for outstanding deposits and payments, reconciled to the balance per the
general ledger at the statement date, with both balances shown.

**Independent Test**: finalise a reconciliation with known outstanding items and confirm the report
shows both balances, the adjusting items, and that the two sides agree (and also with no outstanding
items).

### Tests

**Wave 1 — independent (different files):**

- [x] **T048** [P] [US5] Report test — the rec report shows "balance per bank statement" and "balance per general ledger", carries each adjusting item into the arithmetic, and proves the two sides equal; runs with and without outstanding items · `tests/StageFright.Reports.Tests/Providers/BankReconciliationReportProviderTests.cs`
- [x] **T049** [P] [US5] Integration — finalisation still requires the reconciliation to balance; a finalised reconciliation stays immutable · `tests/StageFright.Core.Tests/Modules/Finance/BankReconciliationServiceTests.cs`
- [x] **T049a** [P] [US5] **Acceptance** — `V28_ConventionalBankReconciliation` drives US5 AC-1…AC-3: finalise a reconciliation with known outstanding deposits + payments, and one with none; asserts the report shows "balance per bank statement" and "balance per general ledger", carries each adjusting item into the arithmetic (not merely lists it), demonstrates the two sides agree, and that a finalised reconciliation is unchanged and non-editable on later view · `tests/StageFright.Integration.Tests/Scenarios/V28_ConventionalBankReconciliationTests.cs`

### Implementation

**Wave 1 — the provider rewrite (single file), then its resources:**

- [x] **T050** [US5] `BankReconciliationReportProvider.BuildAccountSectionsAsync` — rewrite to: balance per bank statement → add outstanding deposits (listed + summed) → less outstanding payments (listed + summed) → adjusted bank balance → balance per general ledger (`GetAccountBalanceAsync(accountId, statementDate)`) → reconciled line; outstanding items from `GetUnreconciledByAccountAsync`; money via `MoneyFormatter` · `src/StageFright.Reports/Providers/BankReconciliationReportProvider.cs`
- [x] **T051** [US5] `ReportsResource` — add `Reports_BankReconciliation_BalancePerBankStatement`, `_AddOutstandingDeposits`, `_LessOutstandingPayments`, `_AdjustedBankBalance`, `_BalancePerGeneralLedger`, `_Reconciled` · `src/StageFright.Reports/Resources/ReportsResource.resx`, `.en-US.resx`, `.fr-FR.resx` *(needs T050)*

**Checkpoint**: the reconciliation report reads the way a bookkeeper or auditor expects and proves
the two balances agree (SC-008).

---

## Phase 8: User Story 6 — Protect reported prior years from back-dated changes (P3)

**Goal**: once a period is closed, the software refuses any financial transaction dated into it,
leaving no partial record.

**Independent Test**: set a closed-through date, then attempt to post fees, payments, expenses and
journals dated before and after it; the earlier ones are rejected with no partial record, the later
ones succeed. Opening balances during first-run setup are still accepted.

### Tests

**Wave 1 — independent (different files):**

- [x] **T052** [P] [US6] Unit — `ClosedPeriodGuard`: null settings, null date, date before / exactly on / after the closed-through date · `tests/StageFright.Core.Tests/Modules/Finance/ClosedPeriodGuardTests.cs`
- [x] **T053** [P] [US6] Integration — for each posting path (fee, payment, expense, income, bank deposit, manual journal, forgiveness), a transaction dated on/before `ClosedThroughDate` leaves no `Fee`/`Payment`/`Transaction`/`JournalEntry` row; a later one posts · `tests/StageFright.Integration.Tests/InternationalAccounting/ClosedPeriodLockTests.cs`
- [x] **T054** [P] [US6] Integration — first-run setup opening balances are accepted regardless (`ClosedThroughDate` is null during setup) · `tests/StageFright.Core.Tests/Modules/Settings/SetupServiceTests.cs`
- [x] **T055** [P] [US6] bUnit — the settings close-period control + confirmation sets `Settings.ClosedThroughDate` · `tests/StageFright.UI.Tests/Pages/Settings/ClosePeriodControlTests.cs`

### Implementation

**Wave 1 — independent (different files):**

- [x] **T056** [P] [US6] `ClosedPeriodException` — `sealed class : Exception`, Constitution §5.2 five-member shape · `src/StageFright.Core/Exceptions/ClosedPeriodException.cs`
- [x] **T057** [P] [US6] `IClosedPeriodGuard` — `Task EnsureOpen(DateTime postingDate, CancellationToken ct = default)` · `src/StageFright.Core/Contracts/IClosedPeriodGuard.cs`

**⟶ then:**

- [x] **T058** [US6] `ClosedPeriodGuard` — depends on `ISettingsRepository`; no-op on null settings / null date; throws `ClosedPeriodException` when `postingDate.Date <= ClosedThroughDate.Value.Date` (`Validation_ClosedPeriod_PostingRejected`) · `src/StageFright.Core/Modules/Finance/ClosedPeriodGuard.cs` *(needs T056, T057)*
- [x] **T059** [US6] `GLRepository` — inject `IClosedPeriodGuard`; call `EnsureOpen` per line in `AddBalancedSetAsync` and `AddPairAsync` before `SaveChangesAsync` · `src/StageFright.Data/Repositories/GLRepository.cs` *(needs T058)*
- [x] **T060** [US6] `UnitOfWork.ExecuteInTransactionAsync` — let `ClosedPeriodException` propagate unwrapped (same pass-through list as `GLBalanceException`) · `src/StageFright.Data/UnitOfWork.cs`
- [x] **T061** [US6] `MauiProgram.RegisterCoreServices` — register `IClosedPeriodGuard → ClosedPeriodGuard` · `src/StageFright.App/MauiProgram.cs` *(needs T058)*

**⟶ then:**

- [x] **T062** [US6] `GeneralSettingsTab` — "close all financial periods through <date>" control (`id="settings-close-through-date"`) + explicit confirmation; `SettingsService.SaveAsync` persists `ClosedThroughDate` · `src/StageFright.UI/Pages/Settings/GeneralSettingsTab.razor` + `.razor.cs`
- [x] **T063** [US6] Finance posting forms — catch `ClosedPeriodException` → `Validation_ClosedPeriod_PostingRejected`; leave the form re-submittable · `src/StageFright.UI/Pages/Finance/JournalEntryPage.razor.cs`, `PaymentForm.razor.cs`, `ExpensePaymentPage.razor.cs`, `RecordIncome.razor.cs`, `BankDepositPage.razor.cs`, `OpeningBalancesWizard.razor.cs`, `src/StageFright.UI/Shared/ReactivationForgivenessDialog.razor.cs`
- [x] **T064** [US6] `ValidationResource` — add `Validation_ClosedPeriod_PostingRejected` · `src/StageFright.Core/Modules/Localization/Resources/ValidationResource.resx`, `.en-US.resx`, `.fr-FR.resx`

**Checkpoint**: a back-dated posting into a closed period is rejected with no business row and no
ledger line (SC-009); setup opening balances still work.

---

## Phase 9: User Story 7 — Choose the financial-year start as a real setup decision (P3)

**Goal**: setup asks when the financial year starts and accepts a non-first-of-month start; every
financial-year report and dashboard figure then uses that start.

**Independent Test**: complete setup choosing a non-first-of-month start, generate the
financial-year-preset reports, confirm the ranges match; an existing AU dataset (July, day 1) is
unchanged.

### Tests

**Wave 1 — independent (different files):**

- [x] **T065** [P] [US7] Unit — `FinancialYearCalculator.GetRange`/`GetPreviousRange` with a non-first-of-month `startDay` and a February start day · `tests/StageFright.Core.Tests/Modules/Finance/FinancialYearCalculatorTests.cs`
- [x] **T066** [P] [US7] Integration — setup with a non-first-of-month start; every FY-preset report honours month + day; an AU (7, 1) dataset's ranges are unchanged · `tests/StageFright.Integration.Tests/InternationalAccounting/FinancialYearStartTests.cs`
- [x] **T067** [P] [US7] bUnit — setup FY-start month + day pickers render, are mandatory, and bind the model · `tests/StageFright.UI.Tests/Pages/Setup/FinancialYearStartPickerTests.cs`

### Implementation

**Wave 1 — the calculator (single file), then its callers:**

- [x] **T068** [US7] `FinancialYearCalculator` — add an optional `int startDay = 1` parameter to `GetRange`/`GetPreviousRange` (the default preserves every existing caller and test); pivot the year on `(month, day)`; range = `start … start.AddYears(1).AddDays(-1)` · `src/StageFright.Core/Modules/Finance/FinancialYearCalculator.cs`
- [x] **T069** [P] [US7] Providers pass `settings.FinancialYearStartDay`: `TrialBalanceReportProvider`, `BalanceSheetReportProvider`, `IncomeStatementReportProvider`, `TaxSummaryReportProvider` · `src/StageFright.Reports/Providers/*.cs` *(needs T068)*
- [x] **T070** [P] [US7] `OpeningBalancesWizard` passes `FinancialYearStartDay` to `FinancialYearCalculator` · `src/StageFright.UI/Pages/Finance/OpeningBalancesWizard.razor.cs` *(needs T068)*

**⟶ then — setup plumbing (independent files):**

- [x] **T071** [P] [US7] `SetupFormModel` — add `int FinancialYearStartMonth` (`[Range(1,12)]`, default `7`) and `int FinancialYearStartDay` (`[Range(1,28)]`, default `1`) · `src/StageFright.UI/Pages/Setup/SetupFormModel.cs`
- [x] **T072** [P] [US7] `SetupRequest` — add `int FinancialYearStartMonth = 7`, `int FinancialYearStartDay = 1` · `src/StageFright.Core/Modules/Settings/SetupRequest.cs`

**⟶ then:**

- [x] **T073** [US7] `SetupService` — validate `FinancialYearStartDay ∈ 1..28` (`Validation_Setup_FinancialYearStartDayRange`); persist month + day · `src/StageFright.Core/Modules/Settings/SetupService.cs` *(needs T072)*
- [x] **T074** [US7] `GeneralAppearanceTab` — mandatory FY-start month `<select id="setup-fy-start-month">` and day `<select id="setup-fy-start-day">` · `src/StageFright.UI/Pages/Setup/Tabs/GeneralAppearanceTab.razor` + `.razor.cs` *(needs T071)*
- [x] **T075** [US7] `SetupResource` / `SettingsResource` — FY-start month/day labels · `src/StageFright.UI/Resources/Strings/SetupResource.resx` (+ `.en-US`, `.fr-FR`), `SettingsResource.resx` (+ `.en-US`, `.fr-FR`)
- [ ] **T076** [US7] Create a follow-on GitHub issue for FR-022 (sub-twelve-month first financial year / part-year label) and note it in [spec.md](spec.md) Assumptions · GitHub issue + `specs/028-international-accounting-standards/spec.md`

**Checkpoint**: setup requires an explicit FY start (month + day), non-first-of-month works, and an
AU dataset's report ranges are unchanged (SC-001, US7 AC-3).

---

## Phase 10: User Story 8 — Retain financial audit history for a defensible period (P3)

**Goal**: a new dataset's audit retention defaults to at least five years and stays configurable, a
failed purge is surfaced not swallowed, and every posting path — including attendance-fee accruals —
leaves an audit-trail entry.

**Independent Test**: check the default retention on a fresh dataset, force a purge failure and
confirm it is surfaced, record attendance that accrues a fee and confirm an audit entry is written.

### Tests

**Wave 1 — independent (different files):**

- [x] **T077** [P] [US8] Integration — a fresh dataset has `AuditRetentionYears == 5`; an existing dataset's configured value survives the migration · `tests/StageFright.Data.Tests/Migrations/AuditRetentionDefaultTests.cs`
- [x] **T078** [P] [US8] Integration — recording attendance that accrues a fee (paid and unpaid) writes an `AuditTrailEntry` for the accrual and for the auto-payment · `tests/StageFright.Core.Tests/Modules/Rehearsals/AttendanceServiceAuditTests.cs`
- [x] **T079** [P] [US8] Integration — a failed startup purge is recorded into the retrievable startup-diagnostic state (not only logged) · `tests/StageFright.Integration.Tests/InternationalAccounting/PurgeFailureSurfacedTests.cs`

### Implementation

**Wave 1 — independent (different files):**

- [x] **T080** [P] [US8] Raise the retention default `1 → 5` at the remaining declaration sites — `SetupFormModel` (default `5`), `SetupRequest` (default `5`), `SetupService` · `src/StageFright.UI/Pages/Setup/SetupFormModel.cs`, `src/StageFright.Core/Modules/Settings/SetupRequest.cs`, `SetupService.cs`  *(entity + config + migration are T003/T005/T006)*
- [x] **T081** [P] [US8] `AttendanceService` — write `IAuditTrailService.LogAsync` entries for the fee accrual and (when paid at creation) the auto-payment, inside the existing transaction · `src/StageFright.Core/Modules/Rehearsals/AttendanceService.cs`
- [x] **T082** [P] [US8] `MauiProgram` — on a failed audit purge, record the failure into the startup-diagnostic state holder in addition to `Log.Error`; startup still continues · `src/StageFright.App/MauiProgram.cs` + the startup-error state type

**⟶ then:**

- [x] **T083** [US8] Surface the purge-failure diagnostic in the UI (the surface that already reads the DB-init startup-failure state — dashboard or settings banner) · `src/StageFright.UI/Layout/*` or `src/StageFright.UI/Pages/Dashboard/*` (`.razor.cs`)

**Checkpoint**: 5-year default on new datasets, existing values preserved, purge failures visible,
attendance accruals audited (SC-010, SC-011).

---

## Phase 11: User Story 9 — Have the accounting policies written down (P3)

**Goal**: a single document states the app's accounting policies, and the finance living spec reflects
the current tax model and is no longer marked draft.

**Independent Test**: open the accounting-policy document and verify each statement against observed
behaviour; confirm the finance living spec drops retired tax concepts and the `[DRAFT]` marker.

### Implementation

**Wave 1 — independent (different files):**

- [ ] **T084** [P] [US9] `docs/accounting-policies.md` — basis of accounting, revenue recognition, rounding, currency, record immutability + reversing-entry corrections, audit-trail retention; states the reports are unaudited management accounts · `docs/accounting-policies.md`
- [ ] **T085** [P] [US9] `capabilities/finance/spec.md` — remove the `> [DRAFT]` line; rewrite the tax requirements and scenarios from the retired registration-based GST/ABN/BAS model (`IsGstRegistered`, `gross ÷ 11`) to the `Settings.IsTaxApplicable` / `TaxRate` / `TaxCode` model (spec 016) · `capabilities/finance/spec.md`

**⟶ then:**

- [ ] **T086** [US9] `capabilities/audit-trail/spec.md` — update the retention figure this feature makes stale: the default is now 5 years (was 1 / "12 months"); the 1–7-year adjustable range and user-configurability are unchanged · `capabilities/audit-trail/spec.md`
- [ ] **T086a** [US9] Note the pre-existing stale living spec `capabilities/settings/spec.md` (retired ABN/GST wording, stale since spec 016) as a follow-up in [spec.md](spec.md) Assumptions or a tracking issue · `specs/028-international-accounting-standards/spec.md`

**Checkpoint**: accounting policies published; the finance living spec is current and non-draft
(SC-012).

---

## Phase 12: User Story 10 — Get a clear plan for internationalising sales tax (P3, spike)

**Goal**: a written assessment of what internationalising the sales-tax feature requires, with a
scoped decision and rough size for each point and follow-on issues for whatever is taken forward — no
implementation, no change to existing tax postings.

**Independent Test**: read the assessment and confirm it records an in-scope / out-of-scope decision
with rough sizing for each required point and that follow-on issues exist for in-scope items.

### Implementation

**Wave 1 — the assessment (single file):**

- [ ] **T087** [US10] `docs/assessments/sales-tax-internationalisation.md` — for rate changes over time, tax-exclusive entry, the balance-sheet classification of recoverable tax (accounts `2310` / `2320`), and whether multiple simultaneous rates or jurisdictions are needed: an in-scope / out-of-scope decision with a rough size each · `docs/assessments/sales-tax-internationalisation.md`

**⟶ then:**

- [ ] **T088** [US10] Create a follow-on GitHub issue for every in-scope point; link each from the assessment · GitHub issues + `docs/assessments/sales-tax-internationalisation.md`
- [ ] **T089** [US10] Verify the branch changes no tax posting mechanic and no stored tax code value (`git diff` review of tax-adjacent code + a stored-value assertion in the AUD regression) · verification against T013

**Checkpoint**: a scoped decision and follow-on issues are recorded for each of the four points
(SC-013, FR-033).

---

## Phase 13: Polish & cross-cutting validation

**Wave 1 — independent:**

- [ ] **T090** [P] Full rebuild + test — `dotnet build -t:Rebuild` then `dotnet test` (no `--no-build`); resolve any warnings a full rebuild surfaces · `StageFrightCommunity.slnx`
- [ ] **T091** [P] `StageFright.Localization.Tests` green — every new user-facing string is localized in the neutral, `en-US`, and `fr-FR` resource sets; no literal regressions · `tests/StageFright.Localization.Tests/`
- [ ] **T091a** [P] Guard — a repo-wide source test asserts no `StageFright.Reports` provider and no `StageFright.UI` money-display site emits a hard-coded `"$"` / `"AUD"` currency literal or formats a money value with `ToString("C")` / `"{0:C}"` / `ToString("F2")`; every displayed, printed, or exported amount routes through `MoneyFormatter` (FR-004, SC-002) · `tests/StageFright.Localization.Tests/CurrencySymbolGuardTests.cs`
- [ ] **T092** [P] Update `CLAUDE.md` (Localization + "money in reports" notes) and finalise any remaining stale `specs/027-localization-support/` lines · `CLAUDE.md`, `specs/027-localization-support/*.md`

**⟶ then:**

- [ ] **T093** Re-run the `AUD` zero-drift regression (T013) against the final build — identical report figures and stored monetary/tax/GL values (SC-004, FR-031, FR-032) · `tests/StageFright.Integration.Tests/InternationalAccounting/AudZeroDriftTests.cs`
- [ ] **T094** Walk SC-001…SC-013 against the running app (a non-AUD, non-first-of-month org end to end) and record evidence · `specs/028-international-accounting-standards/` (evidence note)

---

## Dependencies & Execution Order

**Phase order**: Setup (P1) → Foundational (P2) → US1 → US2 → US3 → US4 → US5 → US6 → US7 → US8 →
US9 → US10 → Polish. US-story phases are in spec priority order (P1 stories first = the MVP slice).

**Phase 1 → Phase 2**: T001 (green baseline) before any change.

**Phase 2 (Foundational)** blocks US1 and the report-money / FY-day work in US3–US5, US7:
Wave 1 `T002 | T003` → Wave 2 `T004 (needs T002) | T005 (needs T003)` → `T006 (needs T005)`,
`T007 (needs T004)` → `T008 (needs T007)`.

**US1**: tests `T009–T014, T014a` (parallel) → `T015 → T016` → setup plumbing `T017 | T018` →
`T019 (needs T018) → T020 → T021 (needs T017)` → report routing `T022 | T023 | T024 | T025 | T026 |
T027` (parallel) → `T028` (docs).

**US2** (independent of US1): tests `T029–T032, T032a` → `T033` → `T034 | T035` (parallel).

**US3**: tests `T036 | T037 | T037a` → `T038 | T039` (parallel) → `T040`.

**US4**: `T041 | T041a` (tests) → `T042` → `T043 | T044 | T045 | T046` (parallel) → `T047`.

**US5**: `T048 | T049 | T049a` (tests) → `T050` → `T051`.

**US6**: tests `T052–T055` → `T056 | T057` → `T058` → `T059 | T060 | T061` → `T062 | T063 | T064`.

**US7**: tests `T065–T067` → `T068` → `T069 | T070` → `T071 | T072` → `T073` → `T074 | T075 | T076`.

**US8**: tests `T077–T079` → `T080 | T081 | T082` (parallel) → `T083`.

**US9**: `T084 | T085` (parallel) → `T086 | T086a` (parallel).

**US10**: `T087` → `T088 | T089`.

**Polish**: `T090 | T091 | T091a | T092` (parallel) → `T093 → T094`.

**Independent stories**: after Phase 2, US2 has no dependency on US1 and can be built alongside it.
US3–US5 depend on US1's `MoneyFormatter` report routing (T022–T027). US6–US10 depend only on the
Foundational `Settings` schema (T003/T005/T006).
