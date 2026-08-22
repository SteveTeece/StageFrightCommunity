# Tasks: Generic International Sales Tax (Replacing ABN & GST)

**Input**: Design documents from `/specs/016-generic-sales-tax/` (plan.md, spec.md, research.md, data-model.md, quickstart.md)

**Tests**: Included throughout — CLAUDE.md and the project constitution (§11) make exhaustive test coverage non-negotiable for this project, not optional per-feature.

**Organization**: Foundational rename work (Phase 2) blocks every user story because the domain types (`TaxCode`, `Settings` fields, `TaxCalculator`) are shared. User stories 3 (P2, "upgrade cleanly") and 4 (P3, "tax report") layer on top once Phase 2 lands; US1 and US2 are the user-facing MVP.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Maps to spec.md's US1–US4
- Paths shown are exact repo-relative paths

---

## Phase 1: Setup

- [X] T001 Confirm baseline: `dotnet build` and `dotnet test` are green on branch `016-generic-sales-tax` before making any change (nothing to fix later gets blamed on this feature).

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: No user story work can begin until this phase is complete — every story depends on these renamed domain types compiling.

- [X] T002 [P] Create `src/StageFright.Core/Enums/TaxCode.cs` (`Taxable`, `TaxExempt`, `Excluded`); delete `src/StageFright.Core/Enums/GstCode.cs`.
- [X] T003 [P] Create `src/StageFright.Core/Modules/Finance/TaxCalculator.cs` (`SplitInclusive(decimal gross, decimal ratePercent)` per data-model.md); delete `GstCalculator.cs` and `GstConstants.cs`.
- [X] T004 [P] Rename the GST GUID/number constants to `Tax*` in `src/StageFright.Core/Modules/Finance/SystemAccounts.cs` (`GstCollectedId`→`TaxCollectedId`, `GstPaidId`→`TaxPaidId`, and their `*Number` constants) — GUID *values* and account numbers unchanged, only the C# names.
- [X] T005 Update `src/StageFright.Core/Entities/Settings.cs`: remove `Abn`; rename `IsGstRegistered`→`IsTaxApplicable`; rename `AnnualFeeGstCode`/`AttendanceFeeGstCode`→`AnnualFeeTaxCode`/`AttendanceFeeTaxCode` (type `TaxCode?`); add `TaxRate` (`decimal?`).
- [X] T006 [P] Update `src/StageFright.Core/Entities/Fee.cs` and `src/StageFright.Core/Entities/Transaction.cs`: rename `GstCode`→`TaxCode` (type `TaxCode?`).
- [X] T007 Update `src/StageFright.Data/Configurations/SettingsConfiguration.cs`, `FeeConfiguration.cs`, `TransactionConfiguration.cs` for the renamed columns and the new `TaxRate` column (depends on T005, T006).
- [X] T008 Delete `src/StageFright.Core/Modules/Settings/AbnValidator.cs` and `AbnAttribute.cs`; delete `tests/StageFright.Core.Tests/Modules/Settings/AbnValidatorTests.cs` and `AbnAttributeTests.cs`.
- [X] T009 Update `src/StageFright.Core/Modules/Settings/SetupRequest.cs`, `SetupService.cs`, `SettingsService.cs`: remove `Abn` handling; validate `TaxRate` required+positive only when `IsTaxApplicable`, null-forced otherwise (mirrors today's GST-code null-forcing) (depends on T005, T008).
- [X] T010 Update `src/StageFright.Core/Modules/Finance/{FeeService.cs,IncomeEntryService.cs,ExpensePaymentService.cs,RecordIncomeRequest.cs,RecordExpenseRequest.cs,OpeningBalanceService.cs}`, `src/StageFright.Core/Modules/Rehearsals/AttendanceService.cs`, `src/StageFright.Core/Modules/Finance/ReactivationForgivenessService.cs`: replace `GstCode`/`GstCalculator`/`SystemAccounts.Gst*` references with `TaxCode`/`TaxCalculator`/`SystemAccounts.Tax*` (depends on T002, T003, T004, T006). `OpeningBalanceService.cs` was an extra file found via a broader `Gst` sweep (the original discovery grep's `\bGst\b` word-boundary missed compound identifiers like `GstCollectedId`); `ReactivationForgivenessService` also gained a new `ISettingsRepository` constructor dependency to read the current `TaxRate` for bad-debt write-off splits.
- [X] T011 Update `src/StageFright.Data/Repositories/SettingsRepository.cs` for any direct `Abn`/GST field references (depends on T005).
- [X] T012 Generate the EF Core migration: `dotnet ef migrations add GenericSalesTax --project src/StageFright.Data/ --startup-project src/StageFright.Data/` (used Data's own `IDesignTimeDbContextFactory` as startup project instead of `StageFright.App`, since App transitively depends on `StageFright.Reports`/`StageFright.UI` which aren't renamed until Phases 4/6 — CLAUDE.md's documented `--startup-project src/StageFright.App/` form will work again once those phases land), then hand-edited the generated `Up()`/`Down()` to add the value-remap `UPDATE` statements (`Fee.TaxCode`, `Transaction.TaxCode`, `Settings.AnnualFeeTaxCode`/`AttendanceFeeTaxCode`, `TaxRate` backfill) and the two system-account `Name` updates, exactly per data-model.md's remap table (depends on T005–T011).
- [X] T013 [P] Added `tests/StageFright.Data.Tests/Migrations/GenericSalesTaxMigrationTests.cs`: applying the migration against a pre-upgrade-shaped seeded row (registered + unregistered `Settings`, an `InputTaxed` `Fee`, a `BasExcluded` `Transaction`) produces exactly the remap in data-model.md, dollar amounts byte-identical, `Abn` column gone (depends on T012).
- [X] T014 [P] Updated `tests/StageFright.Core.Tests/Modules/Finance/{FeeServiceTests,IncomeEntryServiceTests,ExpensePaymentServiceTests,ReactivationForgivenessServiceTests,OpeningBalanceServiceTests}.cs`, `tests/StageFright.Core.Tests/Modules/Rehearsals/AttendanceServiceTests.cs`, and `tests/StageFright.Core.Tests/Modules/Finance/GstCalculatorTests.cs`→`TaxCalculatorTests.cs` to the renamed types/fields, plus a `TaxRate = 10m` addition to each "taxable" test fixture (the old hardcoded 10% rate is no longer implicit) (depends on T010). `AccountBalanceServiceTests.cs` needed no changes — it doesn't reference any GST/tax fields.
- [X] T015 [P] Updated `tests/StageFright.Core.Tests/Modules/Settings/{SetupServiceTests,SettingsServiceTests}.cs` to the renamed fields and removed `Abn`; updated `tests/StageFright.Data.Tests/Settings/SetupServiceIntegrationTests.cs`'s positional `SetupRequest` constructor calls; retired `tests/StageFright.Data.Tests/Migrations/AddAbnToSettingsMigrationTests.cs` (its `settings.Abn == null` premise no longer applies now that `Abn` is fully removed) (depends on T009).

**Checkpoint**: `dotnet build` succeeds; `StageFright.Core.Tests` and `StageFright.Data.Tests` are green. UI/report work can now proceed.

---

## Phase 3: User Story 1 — First-run setup without ABN, with generic sales tax (Priority: P1) 🎯 MVP

**Goal**: A person setting up a new, non-Australian organisation completes the wizard without any ABN prompt, and configures sales tax generically if applicable.

**Independent Test**: quickstart.md Scenario 1.

- [X] T016 [US1] Update `src/StageFright.UI/Pages/Setup/SetupFormModel.cs`: remove `Abn`; rename `IsGstRegistered`→`IsTaxApplicable`; add required-when-applicable `TaxRate`; rename the GST-code properties to `AnnualFeeTaxCode`/`AttendanceFeeTaxCode`. Added an `IValidatableObject.Validate` cross-field check for "TaxRate required and positive while IsTaxApplicable" — a plain `[Range]` on a nullable field never fires for a blank value, so the required-when-applicable rule needed this instead.
- [X] T017 [US1] Update `src/StageFright.UI/Pages/Setup/SetupWizard.razor` and `.razor.cs`: remove the ABN field from the Organisation step; rename the "GST Registration" step to "Sales Tax" with the required rate field and generic wording; rename `HandleGstToggleChanged`→`HandleTaxToggleChanged` to clear rate + both codes on toggle-off; update the `SetupRequest` composition in `HandleValidSubmitAsync`. Deleted the now-orphaned `AbnInput.cs` shared component (Abn-specific digit-grouping input, unused once both callers were removed).
- [X] T018 [US1] Update `tests/StageFright.UI.Tests/Pages/Setup/SetupWizardTests.cs`: remove ABN validation-blocking assertions; replace GST toggle/dropdown assertions with the Sales Tax step's toggle/rate/dropdowns; add coverage for blank/non-positive rate blocking Next. Also fixed two sibling files the full-suite run surfaced still using the removed `#abn` field/`ValidAbn` const: `SetupWizardThemeTests.cs` and `SetupWizardNoSeederTests.cs`.
- [X] T019 [US1] Ran `dotnet test tests/StageFright.UI.Tests/ --filter FullyQualifiedName~SetupWizard` (green). The quickstart.md Scenario 1 manual UI walkthrough was **not** performed interactively in this non-interactive session — bUnit component tests exercise the equivalent wizard flow (no-ABN organisation step, blank/zero-rate blocking Next, full request composition) instead.

**Checkpoint**: User Story 1 is fully functional and independently testable.

---

## Phase 4: User Story 2 — Changing sales tax settings after setup (Priority: P2)

**Goal**: A treasurer changes tax applicability/rate/per-fee treatment from a "Sales Tax" Settings tab after setup, with the same confirm-before-commit and cross-tab save safety as today.

**Independent Test**: quickstart.md Scenario 2.

- [X] T020 [US2] Rename `src/StageFright.UI/Pages/Settings/GstSettingsTab.razor(.cs)` → `TaxSettingsTab.razor(.cs)`: generic wording, tax-rate input, per-fee taxable/exempt dropdowns, same confirm-before-commit toggle pattern and cross-tab re-fetch-before-save logic.
- [X] T021 [US2] Update `src/StageFright.UI/Pages/Settings/GeneralSettingsTab.razor(.cs)`: remove the ABN field and the "ABN not on file" notice.
- [X] T022 [US2] Update `src/StageFright.UI/Pages/Settings/SettingsPage.razor(.cs)`: rename the "GST / BAS" tab registration and its `?tab=` query key to "Sales Tax"/`tax`, keeping the same tab position.
- [X] T023 [US2] Rename `tests/StageFright.UI.Tests/Pages/Settings/GstSettingsTabTests.cs` → `TaxSettingsTabTests.cs`; update `GeneralSettingsTabTests.cs`, `SettingsCrossTabSaveTests.cs`, `SettingsPageTests.cs` to the new tab name/fields. `SettingsCrossTabSaveTests.cs`'s second scenario (previously ABN-based) now exercises an OrganizationName-based stale-copy race, since the ABN field no longer exists.
- [X] T024 [US2] Ran `dotnet test tests/StageFright.UI.Tests/ --filter FullyQualifiedName~Settings` (green). The quickstart.md Scenario 2 manual UI walkthrough was **not** performed interactively — bUnit tests exercise the equivalent Settings-page flow (Sales Tax tab, confirm-before-commit toggle, cross-tab save safety) instead.

**Checkpoint**: Users 1 and 2 both work independently — setup and post-setup changes are fully generic.

---

## Phase 5: User Story 3 — Existing installations upgrade cleanly (Priority: P2)

**Goal**: An upgrading organisation's GST registration/rate/ABN carry forward correctly and every historical financial record keeps its exact amount with a valid tax label.

**Independent Test**: quickstart.md Scenario 3.

- [X] T025 [US3] Renamed `tests/StageFright.Integration.Tests/Scenarios/V15_GstBasTests.cs` → `V16_GenericSalesTaxTests.cs`, exercising the tax-applicable/not-applicable income & expense posting paths and the Tax Summary report against the same $110-in/$110-out/10%-rate fixture the old GST test used. The pre-upgrade-shaped-database half of the upgrade path (Abn + IsGstRegistered + InputTaxed historical rows → post-migration assertions) is covered separately by `tests/StageFright.Data.Tests/Migrations/GenericSalesTaxMigrationTests.cs` (added in the Foundational phase, T013).
- [X] T026 [US3] Verified via automated coverage: `GenericSalesTaxMigrationTests.cs` (Data.Tests) applies the migration against a seeded pre-upgrade row and asserts byte-identical dollar amounts + valid remapped `TaxCode` values. The quickstart.md Scenario 3 manual walkthrough against a hand-seeded SQLite file was **not** performed interactively in this non-interactive session.

**Checkpoint**: Upgrade path verified end-to-end — no data loss, no broken historical records.

---

## Phase 6: User Story 4 — Tax summary reporting reflects the generic model (Priority: P3)

**Goal**: The built-in tax report uses plain English instead of Australian BAS form codes.

**Independent Test**: quickstart.md Scenario 4.

- [X] T027 [US4] Renamed `src/StageFright.Reports/Providers/BasSummaryReportProvider.cs` → `TaxSummaryReportProvider.cs`: `ReportId`/`ReportName` → `"tax-summary"`/`"Tax Summary"`, plain-English 2-column (Description/Amount) rows — Total taxable sales, Total tax-exempt sales, Tax collected on sales, Tax paid on purchases, Net tax payable/refundable grand total — per data-model.md (G11 and the old Label column dropped), a clear "Sales tax does not apply..." message (no dollar figures) when `IsTaxApplicable` is false.
- [X] T028 [US4] Updated `src/StageFright.App/MauiProgram.cs` report-provider registration to the renamed type.
- [X] T029 [US4] Renamed `tests/StageFright.Reports.Tests/BasSummaryReportProviderTests.cs` → `TaxSummaryReportProviderTests.cs`; updated assertions to the new labels/`ReportId`/2-column shape. `BalanceSheetReportProviderTests.cs` needed no changes — it doesn't reference any GST/tax fixture fields.
- [X] T030 [US4] Ran `dotnet test tests/StageFright.Reports.Tests/` (green, no "BAS"/"GST"/"1A"/"G1" wording anywhere in the provider's output). The quickstart.md Scenario 4 manual PDF/CSV walkthrough was **not** performed interactively in this non-interactive session.

**Checkpoint**: All four user stories independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T031 [P] Updated `src/StageFright.App/Seeding/DebugDataSeeder.cs` per FR-017/data-model.md: `CreateAnnualFeeAccrualAsync` reads `IsTaxApplicable`/`AnnualFeeTaxCode`, compares to `TaxCode.Taxable`, calls `TaxCalculator.SplitInclusive(settings.AnnualFee, settings.TaxRate ?? 0m)`, posts to `SystemAccounts.TaxCollectedId`/`TaxCollectedNumber`.
- [X] T032 [P] The quickstart.md Scenario 5 manual Debug-build "Load sample data" walkthrough was **not** performed interactively in this non-interactive session. Verified instead that `DebugDataSeeder.cs` compiles cleanly against the renamed `Settings`/`TaxCode`/`SystemAccounts` members and that the full solution builds/tests green with the seeder included.
- [X] T033 Swept `src/StageFright.UI/Pages/Finance/{RecordIncome*,ExpensePayment*}` and renamed all remaining GST labels/dropdowns/hints to generic tax wording (`RecordIncomeModel.cs`, `ExpensePaymentModel.cs`, `RecordIncome.razor(.cs)`, `ExpensePaymentPage.razor(.cs)` — `_isGstRegistered`→`_isTaxApplicable` + new `_taxRate` field, `GstInclusiveHint`→`TaxInclusiveHint` using `TaxCalculator.SplitInclusive(amount, rate)`). Also deleted the now-orphaned `AbnInput.cs`/`AbnInputTests.cs` (ABN-specific input component, unused once the Setup/General-tab ABN fields were removed).
- [X] T034 Updated CLAUDE.md: Reports pipeline list (`BasSummary`→`TaxSummary`).
- [X] T035 [P] Swept the solution for leftover `Gst`/`Abn`/`Bas` identifiers. Zero matches remain in production code (`src/`) outside historical EF migration files (`AddGst`, `AddAbnToSettings`, and older `.Designer.cs` snapshots — correctly never edited retroactively). The three remaining `tests/` matches are legitimate: `GenericSalesTaxMigrationTests.cs` and `AddAuditRetentionYearsToSettingsMigrationTests.cs` intentionally construct old-schema raw-SQL fixture rows to test migrations against pre-upgrade data, and `TaxCalculatorTests.cs` has one doc-comment referencing the retired `GstCalculator` for historical compatibility context.
- [X] T036 Full verification: `dotnet build` succeeded (0 errors) and `dotnet test` (without `--no-build`) passed 1464/1464 across all five test projects (557 Core, 138 Reports, 450 UI, 148 Data, 171 Integration), per CLAUDE.md Build & Test Verification.

---

## Dependencies & Execution Order

- **Setup (T001)**: no dependencies.
- **Foundational (T002–T015)**: blocks every user story; T012 (migration) depends on all preceding entity/config renames; T013–T015 (tests) depend on their respective production-code tasks.
- **US1 (T016–T019)**, **US2 (T020–T024)**: both depend only on Foundational; independent of each other.
- **US3 (T025–T026)**: depends on Foundational (specifically T012's migration); independent of US1/US2.
- **US4 (T027–T030)**: depends on Foundational (T002, T003, T004, T010 for the GL math it reads); independent of US1/US2/US3.
- **Polish (T031–T036)**: T031/T032 depend on Foundational; T036 depends on everything else being complete.

## Parallel Example: Foundational Phase

```
T002 (TaxCode enum), T003 (TaxCalculator), T004 (SystemAccounts) can run together — different files, no cross-dependencies.
Once T005/T006 land, T007 (EF configurations) and T008 (delete Abn validator+tests) can run together.
```

## Implementation Strategy

**MVP first**: T001 → Phase 2 (Foundational) → Phase 3 (US1) → stop and validate quickstart.md Scenario 1. That alone unblocks non-Australian organisations from completing setup, which is the core issue #300 ask.

**Incremental delivery**: Foundational → US1 (MVP) → US2 → US3 → US4 → Polish, validating each story's quickstart scenario before moving on.
