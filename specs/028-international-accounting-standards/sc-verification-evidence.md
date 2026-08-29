# SC-001…SC-013 verification evidence (T094)

**Feature**: `028-international-accounting-standards` · **Recorded**: 2026-08-30 ·
**Build under test**: full `dotnet build -t:Rebuild` — 0 warnings, 0 errors ·
**Suite under test**: full `dotnet test` — 2002 passed, 0 failed, 0 skipped
(Core 714, Localization 28, Reports 236, UI 624, Data 156, Integration 244).

## Method

Each success criterion is exercised end to end by an automated acceptance scenario
(`tests/StageFright.Integration.Tests/Scenarios/V28_*`) and/or a cross-layer integration
test (`tests/StageFright.Integration.Tests/InternationalAccounting/*`). These drive the
real service layer, the real `StageFrightDbContext` (SQLite), the real double-entry GL
posting path, and the real `IReportProvider` → `ReportData` → `PdfReportRenderer` /
`CsvReportExporter` pipeline — the same code the MAUI shell runs. The currency-, FY-start-
and locale-sensitive scenarios build a **non-`AUD`, non-first-of-month organisation**
(`JPY` zero-decimal and `EUR`/`USD` under `fr-FR` / `de-DE`, FY start e.g. 6 April) from
first-run setup, then post fees, payments, expenses, journals and reconciliations and
generate every financial report against it.

Focused re-runs recorded for this note:

| Command | Result |
|---|---|
| `dotnet test … --filter "FullyQualifiedName~V28_ \| FullyQualifiedName~InternationalAccounting"` | **52 passed, 0 failed** |
| `dotnet test … --filter "FullyQualifiedName~AudZeroDrift \| FullyQualifiedName~V28_CurrencyConfiguration"` | **9 passed, 0 failed** (T093) |
| `dotnet test tests/StageFright.Localization.Tests/` | **28 passed, 0 failed** (T091, incl. new `CurrencySymbolGuardTests` ×3) |

A hand-driven CDP walkthrough of the packaged MAUI app was **not** performed for this
pass; the acceptance suite above covers the identical code paths for a non-`AUD`,
non-first-of-month organisation, including report PDF/CSV rendering.

## Criteria

| SC | Statement (abridged) | Evidence | Result |
|---|---|---|---|
| **SC-001** | Non-AU currency **and** non-first-of-month FY start chosen once at setup; no later setting change needed for correct money display and reporting periods | `V28_CurrencyConfigurationTests` (setup persists currency; every report uses it) + `InternationalAccounting/FinancialYearStartTests.Should_PersistANonFirstOfMonthFinancialYearStart_When_ChosenAtSetup` + `Should_BoundIncomeStatementAndTrialBalance_OnTheConfiguredAnchorDay` / `Should_BoundBalanceSheetAsAt_TheConfiguredYearEndDay` / `Should_BoundTaxSummaryQuarter_OnTheConfiguredAnchorDay` | ✅ Pass |
| **SC-002** | With any shipped region active, **0** monetary values anywhere show a symbol/code other than the configured one | `V28_CurrencyConfigurationTests.EveryFinancialReport_ShowsTheConfiguredCurrency_AndNeverDollarOrAud` + `RegionalFormatting_KeepsTheConfiguredSymbol_UnderAForeignCulture`; source guard `Localization.Tests/CurrencySymbolGuardTests` (no hard-coded `"$"` / `"AUD"` / `ToString("C"\|"F2")` in any report provider or UI money site) + `Us2LocalizationGuardTests.Should_NotUseCFormat_When_AnyDisplaySiteScanned` (repo-wide) | ✅ Pass |
| **SC-003** | Local representation of "one and a half" into **any** money field — incl. manual journal and opening balances — stores exactly `1.5` in 100% of regions | `V28_LocaleSafeMoneyEntryTests` (`fr-FR`, `de-DE`: journal line and opening balance store the exact ledger value; identical to `en-AU` for the same input) + `Core.Tests/Localization/MoneyInputTests` + `Localization.Tests/MoneyInputGuardTests` | ✅ Pass |
| **SC-004** | 100% of existing AUD reference datasets produce identical report figures **and** identical stored values before/after | `InternationalAccounting/AudZeroDriftTests` (`IncomeStatement_FiguresAreTheSameNumbers…`, `TrialBalance_TiesAndShowsTheSameTotals`, `StoredTransactionValues_AreUntouched`, `MoneyFormatter_ForAud_IsByteIdenticalToTheLegacyDollarString`, `TaxCalculator_AtTwoMinorDigits_IsUnchanged`) + `V28_CurrencyConfigurationTests.SameDataset_UnderAud_IsByteIdenticalToTheLegacyDollarString` — re-run against the final build (T093) | ✅ Pass |
| **SC-005** | Balance Sheet from an unbalanced ledger fails/flags in 100%; a balanced ledger ties in 100% | `V28_StatementIntegrityTests.CorruptedLedger_BalanceSheet_ShowsExplicitOutOfBalanceLine_AndNeverACleanStatement` + `BalancedLedger_BalanceSheet_ProducesACleanStatement`; `Reports.Tests/BalanceSheetReportProviderTests` | ✅ Pass |
| **SC-006** | A Trial Balance whose debits and credits differ by one cent fails to generate | `V28_StatementIntegrityTests.TrialBalance_WithAOneCentImbalance_StillFailsToGenerate` + `CorruptedLedger_TrialBalance_FailsToGenerate`; `Reports.Tests/TrialBalanceReportProviderTests.GenerateAsync_WhenDebitsAndCreditsDifferByOneCent_ThrowsGLBalanceException` (no tolerance band) | ✅ Pass |
| **SC-007** | 100% of financial statements display an accurate basis-of-accounting line | `V28_BasisOfAccountingDisclosureTests` (screen, PDF, CSV each carry the line; wording names **both** the accrual treatment of member fees and the cash treatment of other income/expenditure) + `Reports.Tests/BasisOfAccountingTests` + `UI.Tests/ReportViewerBasisTests` | ✅ Pass |
| **SC-008** | Bank reconciliation report shows both "balance per bank statement" and "balance per general ledger" and demonstrates equality on every finalised reconciliation | `V28_ConventionalBankReconciliationTests` (`Report_ShowsBalancePerBankStatement_AndBalancePerGeneralLedger`, `OutstandingItems_AreListed_AndCarriedIntoTheAdjustedBankBalance`, `AdjustedBankBalance_EqualsBalancePerGeneralLedger_TheTwoSidesAgree`, `WithNoOutstandingItems_…`, `FinalisedReconciliation_IsImmutable_AndFinalisationRequiredItToBalance`) | ✅ Pass |
| **SC-009** | A financial transaction dated into a closed period is rejected in 100% of attempts, with no partial record | `InternationalAccounting/ClosedPeriodLockTests` — `Should_RejectAndPersistNothing_When_PostingIsDatedInTheClosedPeriod` across `expense`/`deposit`/`journal`/`income`, plus payment, fee-accrual and forgiveness paths; `Should_Post_When_DatedAfterTheClosedPeriod` for the open side; setup opening balances still accepted (`Should_PostAnyBackdatedEntry_When_NoPeriodIsClosed`) | ✅ Pass |
| **SC-010** | Audit-retention default on a new dataset is at least five years | `Data.Tests/Migrations/AuditRetentionDefaultTests.FreshDataset_DefaultsAuditRetentionYearsToFive_AfterMigration` + `Core.Tests/…/SetupServiceTests.InitializeAsync_PersistsDefaultAuditRetentionYears_WhenNotSpecified`; existing configured values preserved through the migration (`migrationBuilder` sets the default constraint only, no `UpdateData`) | ✅ Pass |
| **SC-011** | Recording attendance that accrues a fee produces an audit-trail entry in 100% of cases | `Core.Tests/Modules/Rehearsals/AttendanceServiceAuditTests` (audit entry written for the fee accrual and, when paid at creation, the auto-payment, inside the one transaction) | ✅ Pass |
| **SC-012** | The accounting-policy document exists and every statement in it matches observed behaviour | [`docs/accounting-policies.md`](../../docs/accounting-policies.md) published (T084) — basis of accounting, revenue recognition, rounding, currency, record immutability + reversing-entry corrections, audit-trail retention, and the "unaudited management accounts" statement; each claim corresponds to a behaviour asserted by the suites above. Finance living spec de-drafted and moved to the current `IsTaxApplicable` / `TaxRate` / `TaxCode` model (T085); `capabilities/audit-trail/spec.md` retention figure corrected to 5 years (T086) | ✅ Pass |
| **SC-013** | The sales-tax internationalisation assessment records a scoped decision for each of its four required points | [`docs/assessments/sales-tax-internationalisation.md`](../../docs/assessments/sales-tax-internationalisation.md) (T087) — rate history / effective-dating (**out**, L), multiple simultaneous rates / jurisdictions (**out**, XL), tax-exclusive amount entry (**in**, M), balance-sheet classification of recoverable input tax `2310` / `2320` (**in**, S–M); FR-033 non-regression confirmed by `git diff` review (T089) and the `AudZeroDriftTests` stored-value assertions | ✅ Pass |

## Residual / carried forward

- **T076 / T088 — follow-on GitHub issues filed 2026-08-30** against parent #341:
  - [#353](https://github.com/SteveTeece/StageFrightCommunity/issues/353) — *"[FEATURE] Support a sub-twelve-month first financial year, labelled as a part-year"* (FR-022, follows #352).
  - [#354](https://github.com/SteveTeece/StageFrightCommunity/issues/354) — *"[FEATURE] Support tax-exclusive amount entry (net + tax) alongside the current tax-inclusive entry"* (follows spike #350).
  - [#355](https://github.com/SteveTeece/StageFrightCommunity/issues/355) — *"[FEATURE] Classify recoverable input tax (account `2320`) correctly on the Balance Sheet"* (follows spike #350).
  - [#356](https://github.com/SteveTeece/StageFrightCommunity/issues/356) — *"[DOCS] Update `capabilities/settings/spec.md` to the current tax model"* (T086a).
- **Issues #341 and sub-issues #342–#352 closed 2026-08-30**, each with a comment pointing at the delivering user story / tasks (delivery is on branch `028-international-accounting-standards`, pending merge to `master`).
- **FR-022 itself** (short first financial year) is intentionally **not implemented** in this
  feature — US7 delivered only the month + day FY-start choice; it is now tracked by #353.
