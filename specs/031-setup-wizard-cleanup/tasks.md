# Tasks: Setup Wizard Layout Cleanup

**Feature dir**: `specs/031-setup-wizard-cleanup/` | **Branch**: `031-setup-wizard-cleanup` | **Size**: `normal`
**Inputs**: `plan.md`, `setup-wizard-cleanup.spec.md`, `research.md`, `contracts/setup-wizard-ui-contract.md`

Line format: `- [ ] **T###** [P?] [US#] Description · exact/file/path`
`[P]` = independent of the other tasks in its wave (different file, no incomplete dependency). `[US#]` maps to a user story in the spec.

Element ids do not change, so existing selectors keep working. Every new label ships translated in the same change (CLAUDE.md localization rule).

---

## Phase 1: Foundation (blocks everything below)

**Wave 1 — independent (different files):**

- [x] **T001** [P] [US2] Add the seven new `Setup_*` keys from the UI contract (`Setup_Tab_Accounting`, `Setup_Review_OrganisationGroupHeading`, `Setup_Review_AccountingGroupHeading`, `Setup_Review_CurrencyTerm`, `Setup_Review_FinancialYearStartTerm`, `Setup_Review_InceptionDateTerm`, `Setup_Review_TaxEntryModeTerm`) to the neutral file and a real translation to each of `de-DE`, `en-US`, `es-ES`, `fr-FR`, `it-IT`, `ja-JP`, `pl-PL`; then regenerate `qps-ploc` with `scripts/generate-pseudo-locale.py` · `src/StageFright.UI/Resources/Strings/SetupResource*.resx`
- [x] **T002** [P] [US2] Create `AccountingTab.razor` and `AccountingTab.razor.cs` (`Model` parameter, injected `IStringLocalizer<SetupResource>`): move the currency, financial-year month/day and inception-date markup from `GeneralAppearanceTab`, move the annual and attendance fee markup from `MembershipFeesTab`, then render `<SalesTaxTab Model="Model" />` inside a `row g-3 mb-3` wrapper · `src/StageFright.UI/Pages/Setup/Tabs/AccountingTab.razor`, `src/StageFright.UI/Pages/Setup/Tabs/AccountingTab.razor.cs`
- [x] **T003** [P] [US1] Trim `GeneralAppearanceTab` to the organisation name only and fix its doc comment · `src/StageFright.UI/Pages/Setup/Tabs/GeneralAppearanceTab.razor`, `src/StageFright.UI/Pages/Setup/Tabs/GeneralAppearanceTab.razor.cs`
- [x] **T004** [P] [US1] Trim `MembershipFeesTab` to membership renewal month and audit retention only (drop the fee inputs) and fix its doc comment · `src/StageFright.UI/Pages/Setup/Tabs/MembershipFeesTab.razor`, `src/StageFright.UI/Pages/Setup/Tabs/MembershipFeesTab.razor.cs`

---

## Phase 2: Wizard and Review (US1, US2, US3)

**Wave 2 — after Phase 1; different files:**

- [x] **T005** [US1][US2][US3] Rework `SetupWizard`: Organisation tab renders `GeneralAppearanceTab`, `MembershipFeesTab` and `ThemeSelectionTab`; insert an Accounting tab at index 1 (title `Setup_Tab_Accounting`) rendering `AccountingTab`; extend `_tabShown` to six entries and renumber every later `SetActiveTab(n)`; remove the old `SalesTaxTab` row from tab 0 · `src/StageFright.UI/Pages/Setup/SetupWizard.razor`, `src/StageFright.UI/Pages/Setup/SetupWizard.razor.cs`
- [x] **T006** [P] [US3] Rework `ReviewTab`: two headed groups (Organisation, Accounting) matching the tabs, add currency, financial-year start, inception date and tax entry mode rows, keep committee rows and both bordered lists · `src/StageFright.UI/Pages/Setup/Tabs/ReviewTab.razor`, `src/StageFright.UI/Pages/Setup/Tabs/ReviewTab.razor.cs`

---

## Phase 3: Sample data guarantee (US4, US5)

**Wave 3 — independent of Phase 2:**

- [x] **T007** [P] [US4][US5] Audit the first-run path: confirm the placeholder `SetupRequest` in `FirstRunLanguageScreen` and the settings written by `DebugDataSeeder.ApplyGeneratedOrganisationSettingsAsync` leave every setting a wizard tab asks for valid (currency, financial year, inception date, tax, audit retention, renewal months, seat target, committee roles). Patch the seeder only if a gap is found; record the audit result in the task close note · `src/StageFright.UI/Pages/Setup/FirstRunLanguageScreen.razor.cs`, `src/StageFright.App/Seeding/DebugDataSeeder.cs`

---

## Phase 4: Tests

**Wave 4 — after Phases 2 and 3; different files:**

- [x] **T008** [P] [US2] Add `AccountingTabTests` (all FR-003 fields present, tax detail hidden then shown with the switch, blank tax rate fails validation, toggle-off clears values) and repoint the picker tests (`CurrencyPickerTests`, `FinancialYearStartPickerTests`, `InceptionDatePickerTests`) and `SalesTaxTabTests` to the component that now hosts each field · `tests/StageFright.UI.Tests/Pages/Setup/Tabs/AccountingTabTests.cs`, `tests/StageFright.UI.Tests/Pages/Setup/CurrencyPickerTests.cs`, `tests/StageFright.UI.Tests/Pages/Setup/FinancialYearStartPickerTests.cs`, `tests/StageFright.UI.Tests/Pages/Setup/InceptionDatePickerTests.cs`, `tests/StageFright.UI.Tests/Pages/Setup/Tabs/SalesTaxTabTests.cs`
- [x] **T009** [P] [US1] Update `GeneralAppearanceTabTests` and `MembershipFeesTabTests` for the trimmed fields (no accounting ids present on either) · `tests/StageFright.UI.Tests/Pages/Setup/Tabs/GeneralAppearanceTabTests.cs`, `tests/StageFright.UI.Tests/Pages/Setup/Tabs/MembershipFeesTabTests.cs`
- [x] **T010** [P] [US3] Update `ReviewTabTests` for the two groups and the four new rows · `tests/StageFright.UI.Tests/Pages/Setup/Tabs/ReviewTabTests.cs`
- [x] **T011** [US1][US2][US3][US5] Update `SetupWizardTests` (and `SetupWizardThemeTests` if it indexes tabs): six-tab order, Next walks all six with Finish only on Review, both tabs' values preserved when moving back, invalid Accounting field blocks Next and Finish, no `languageSelect` or `seedData` in any tab, no accounting id on the Organisation tab; adjust the `AdvanceFromOrganisationSettings` / `AdvanceToReview` helpers for the extra tab · `tests/StageFright.UI.Tests/Pages/Setup/SetupWizardTests.cs`, `tests/StageFright.UI.Tests/Pages/Setup/SetupWizardThemeTests.cs`
- [x] **T012** [P] [US4][US5] Add or extend `FirstRunLanguageScreenTests`: the language selector precedes the sample-data switch, the switch is absent without a seeder, and confirming with sample data builds a valid request, runs the seeder, and navigates to the dashboard with no wizard step · `tests/StageFright.UI.Tests/Pages/Setup/FirstRunLanguageScreenTests.cs`

---

## Phase 5: Docs and verification

**Wave 5 — after Phase 4:**

- [x] **T013** [P] Update docs that describe the wizard layout: the tab list in `specs/017-setup-wizard-tabs/spec.md` and its UI contract, the "Organisation Settings" mention in `capabilities/app-host/spec.md`, and any wording in `specs/022-seed-data-placement/` that places fields on the first tab; add a one-line "superseded by spec 031" note rather than rewriting history · `specs/017-setup-wizard-tabs/spec.md`, `specs/017-setup-wizard-tabs/contracts/setup-wizard-ui-contract.md`, `capabilities/app-host/spec.md`, `specs/022-seed-data-placement/spec.md`
- [x] **T014** Run `dotnet build` and the full `dotnet test` (no `--no-build`), including the localization guard tests; report results. Re-run `StageFright.UI.Tests` once in isolation if only the known `fee` GUID flakes fail · solution root

---

## Dependencies

- T005 needs T002, T003, T004 (and T001 for the tab title key).
- T006 needs T001.
- T008 to T011 need T005 and T006; T012 needs T007.
- T014 is last.

## Coverage

| Requirement | Tasks |
|-------------|-------|
| FR-001, FR-010 | T005, T011 |
| FR-002, FR-003, FR-004, FR-005, FR-006 | T002, T003, T004, T008, T009 |
| FR-007, FR-008 | T005, T011 |
| FR-009 | T006, T010 |
| FR-011, FR-012 | T011, T012 |
| FR-013, FR-014 | T007, T012 |
| FR-015 | T001, T014 |
| FR-016 | T013 |
