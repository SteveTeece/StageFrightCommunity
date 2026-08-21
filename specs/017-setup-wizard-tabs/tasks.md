# Tasks: Setup Wizard Tabbed Redesign

**Input**: Design documents from `/specs/017-setup-wizard-tabs/` (plan.md, spec.md, research.md, data-model.md, contracts/setup-wizard-ui-contract.md)

**Tests**: Included throughout — CLAUDE.md's "Exhaustive code-path test coverage" rule is project-wide and non-negotiable, not opt-in per feature. The one deliberate exception (T036, DebugDataSeeder) is noted inline with its rationale.

**Organization**: Grouped by user story (spec.md US1–US6), but **phase order follows the real dependency chain the spec itself documents, not raw priority order**. Spec priorities are P1(US1), P1(US2), P2(US3), P2(US4), P2(US5), P3(US6) — but US2's own "Why this priority" text says it "depends on User Story 1's tabs and on User Story 4's Chart of Accounts tab (a queued account needs a row here too)", and US3 needs both US4's and US5's queued-list state to summarize on the Review tab. You cannot build a P1 story before a P2 prerequisite it explicitly depends on, so the phases below run **US1 → US4 → US2 → US5 → US3 → US6**: US4 (accounts) is pulled forward because US2 (opening balances, P1) genuinely can't be finished without it; US5/US3 stay after the P1 pair since nothing in P1 depends on them; US6 (control-style polish, explicitly "the first thing that could be deferred" per its own spec text) runs last. Each `[US#]` tag still maps to the spec's own story numbering for traceability.

## Format: `[ID] [P?] [Story] Description · file`

- **[P]**: Independent of the other tasks in its wave — different file, no incomplete dependency — buildable in any order (or in parallel).
- **[US#]**: Maps to spec.md's US1–US6.
- A **wave** groups tasks that can be built in any order; **⟶** join lines mark a hard wait for the previous wave.

---

## Phase 1: Setup

- [x] **T001** Confirm baseline: `dotnet build` and `dotnet test` (no `--no-build`) are green on branch `017-setup-wizard-tabs` before any change, per CLAUDE.md's Build & Test Verification rule.

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: No wizard-tab story (US1–US6) can begin until this phase is complete — every tab either renders inside the new shared `BorderedListBox`, submits through one of the two extracted shared forms, or relies on `SetupRequest`/`SetupService` already knowing how to carry and create queued state.

**Wave 1 — independent (different files):**

- [x] **T002** [P] Create `BorderedListBox<TItem>` · `src/StageFright.UI/Shared/BorderedListBox.razor(.cs)` — bordered container, `Items`/`RowTemplate`/optional `OnRemove` (unset ⇒ read-only)/`EmptyText` per the UI contract; internal scroll so a long list stays contained (Edge Cases: overflow must not push the rest of the tab out of view) (FR-007).
- [x] **T003** [P] Create `AddAccountForm` · `src/StageFright.UI/Shared/AddAccountForm.razor(.cs)` — extracted from `ChartOfAccountsPage`'s inline add-account markup/`NewAccountModel`: name/type/Asset-only bank-flag fields, `DataAnnotationsValidator`, required `OnSubmit` callback, `SubmitButtonText` param (FR-012, FR-016). **Gap the contract leaves open**: it doesn't say how in-component duplicate-name checking gets its comparison set (immediate mode needs real account names; queued mode needs real + already-queued names) — add an `ExistingNames: IReadOnlyList<string>` (or equivalent) parameter and record the addition back into `contracts/setup-wizard-ui-contract.md` (T051 double-checks this).
- [x] **T004** [P] Create `OpeningBalanceEntryForm` · `src/StageFright.UI/Shared/OpeningBalanceEntryForm.razor(.cs)` — extracted from `OpeningBalancesWizard`'s balance-entry table (Step 2/3 markup + `OpeningBalanceRowModel`/plug logic): `Accounts`, required `OnSubmit`, `ShowAlreadyPostedWarning` (default `true`) per the UI contract (FR-017, FR-019).
- [x] **T005** [P] Extend `SetupRequest.cs` · `src/StageFright.Core/Modules/Settings/SetupRequest.cs` — add optional `QueuedAccounts: IReadOnlyList<QueuedAccountRequest>?`, `QueuedOpeningBalances: IReadOnlyList<OpeningBalanceEntry>?`, `OpeningBalanceAsAtDate: DateTime` (default `UtcNow.Date`), and a new `QueuedAccountRequest` record. **Resolves a data-model.md inconsistency**: its "Account reference resolution" paragraph says `SetupWizard.razor.cs` resolves queued-account references, but its own "SetupService.InitializeAsync (extended orchestration)" section — confirmed by research.md's decision — has `SetupService` do it internally. Follow the latter: give `QueuedAccountRequest` a `ClientId: Guid` local reference, and have the wizard use that same `Guid` as `OpeningBalanceEntry.AccountId` for a queued account's balance row until Finish remaps it (T008).

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — independent (different files):**

- [x] **T006** [P] Refactor `ChartOfAccountsPage.razor(.cs)` · `src/StageFright.UI/Pages/Finance/` — consume the shared `AddAccountForm` (T003) with an immediate-create `OnSubmit` (`AccountService.CreateAsync`, unchanged behavior); remove the inlined markup/`NewAccountModel` it replaces (FR-016).
- [x] **T007** [P] Refactor `OpeningBalancesWizard.razor(.cs)` · `src/StageFright.UI/Pages/Finance/` — consume the shared `OpeningBalanceEntryForm` (T004) for its Step 2 table with `ShowAlreadyPostedWarning="true"` (unchanged behavior) (FR-019).
- [x] **T008** [P] Extend `SetupService.cs` · `src/StageFright.Core/Modules/Settings/SetupService.cs` — inject `IAccountService`/`IOpeningBalanceService`; extend `InitializeAsync` per data-model.md's sequence: after Settings + default event types, (1) create each `QueuedAccounts` entry via `IAccountService.CreateAsync`, mapping `ClientId → real Account.Id`; (2) if `QueuedOpeningBalances` is non-empty, remap any `ClientId`-keyed entries to their real `AccountId` and call `IOpeningBalanceService.RecordOpeningBalancesAsync` once with `OpeningBalanceAsAtDate`; (3) existing committee-office-holder-title loop, unchanged (FR-008, FR-013, FR-018).

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — independent (different files):**

- [x] **T009** [P] New `tests/StageFright.UI.Tests/Shared/BorderedListBoxTests.cs` — items render via `RowTemplate`; `OnRemove` unset renders read-only, set renders a remove affordance per row; `EmptyText` shown when empty; overflow stays contained.
- [x] **T010** [P] New `tests/StageFright.UI.Tests/Shared/AddAccountFormTests.cs` — valid submit invokes `OnSubmit`; blank name rejected; duplicate name (against `ExistingNames`, case-insensitive) rejected; bank-flag checkbox only shown for `Asset`; `SubmitButtonText` renders.
- [x] **T011** [P] New `tests/StageFright.UI.Tests/Shared/OpeningBalanceEntryFormTests.cs` — one row per account; negative amount accepted (Edge Cases: posts to the opposite normal side); plug recalculates live; `ShowAlreadyPostedWarning` toggles the warning banner; `OnSubmit` receives the built `RecordOpeningBalancesRequest`.
- [x] **T012** [P] Update `tests/StageFright.UI.Tests/Pages/Finance/ChartOfAccountsPageTests.cs` for the `AddAccountForm`-based refactor (T006) — same behavior, adjust selectors only where the extraction changed them.
- [x] **T013** [P] Update `tests/StageFright.UI.Tests/Pages/Finance/OpeningBalancesWizardTests.cs` for the `OpeningBalanceEntryForm`-based refactor (T007).
- [x] **T014** [P] Update `tests/StageFright.Core.Tests/Modules/Settings/SetupServiceTests.cs` — extend `CreateService()` for the two new constructor deps (`IAccountService`/`IOpeningBalanceService` substitutes); add coverage for: empty queues (no-op, today's behavior unchanged), non-empty `QueuedAccounts` (each created, `ClientId` mapped), non-empty `QueuedOpeningBalances` with a `ClientId` reference resolved to the just-created account's real id, and an already-existing-account reference passed through unchanged.
- [x] **T015** [P] Update `tests/StageFright.Integration.Tests/Scenarios/V1_FirstRunSetupTests.cs`'s `BuildSetupService()` for `SetupService`'s new constructor (real `AccountService`/`OpeningBalanceService` instances against the in-memory SQLite `_db`, matching the file's existing style) — no new scenario yet, just keeps it compiling.

**Checkpoint**: `dotnet build` succeeds; Core/UI test projects are green. The shared components exist, the standalone Finance pages behave exactly as before, and `SetupService` can create queued accounts/opening balances when asked. Wizard-tab story work can now proceed.

---

## Phase 3: User Story 1 — Complete first-run setup from one tabbed screen (Priority: P1) 🎯 MVP

**Goal**: The setup wizard is one screen with a `Tabs`/`Tab` strip (matching `FinancePage.razor`), replacing the linear 5-step flow, with every existing field/rule/default (FR-024) relocated into logically grouped tabs.

**Independent Test**: Launch with no Settings record, walk every tab via both direct click and Next, fill required fields, finish from the Review tab, land on the dashboard — one screen shell throughout.

**Wave 1 — independent (different files):**

- [x] **T016** [P] [US1] Create `GeneralAppearanceTab.razor(.cs)` · `src/StageFright.UI/Pages/Setup/Tabs/` — organisation name + the theme control, carried over as-is (still `RadzenSwitch` — US6/T049 swaps it to a dropdown later, keeping this story a pure regrouping).
- [x] **T017** [P] [US1] Create `MembershipFeesTab.razor(.cs)` · `src/StageFright.UI/Pages/Setup/Tabs/` — annual fee, attendance fee, membership renewal month, audit retention years, relocated unchanged.
- [x] **T018** [P] [US1] Create `SalesTaxTab.razor(.cs)` · `src/StageFright.UI/Pages/Setup/Tabs/` — tax-applicable checkbox, tax rate, fee tax-treatment dropdowns, relocated unchanged including the existing `HandleTaxToggleChanged` clear-on-toggle-off behavior (Edge Cases).
- [x] **T019** [P] [US1] Create `CommitteeTab.razor(.cs)` · `src/StageFright.UI/Pages/Setup/Tabs/` — AGM month, general committee seat count target, and the **existing** comma-separated `CommitteeOfficeHolderTitlesText` field carried over unchanged (US5/T041 replaces it with the +/− widget later).
- [x] **T020** [P] [US1] Create `ReviewTab.razor(.cs)` · `src/StageFright.UI/Pages/Setup/Tabs/` — the existing `dl`-based read-only summary of every setting, plus the existing debug-only "Load sample data" checkbox relocated here (it already lives on today's last step, so FR-025 is satisfied by this move alone) (FR-005, FR-025).

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2:**

- [x] **T021** [US1] Rewrite `SetupWizard.razor(.cs)` · `src/StageFright.UI/Pages/Setup/` — host `<Tabs><Tab Title="…" OnShown="…">` for the 5 tabs above (T016–T020), matching `FinancePage.razor`'s pattern; tab-click AND Next both navigate (FR-003); Next validates only the current tab's own fields and refuses to advance on failure (FR-004); Finish keeps validating the **whole** `_editContext` (already does today) and, on failure, indicates which tab needs attention rather than a bare error (Edge Cases); Finish orchestration (`SetupService.InitializeAsync` + optional debug seeding + navigate to `/dashboard`) is otherwise unchanged from today. **Known gotcha** (CLAUDE.md): watch for concurrent-DbContext issues from `Tabs`/`Tab`'s `OnShown` firing during rapid tab switching — no tab does DB work on `OnShown` yet at this point in the build, so confirm none is introduced.

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — independent (different files):**

- [x] **T022** [P] [US1] Rewrite `tests/StageFright.UI.Tests/Pages/Setup/SetupWizardTests.cs` for the tabbed flow — tab-click navigation, Next gating per tab, Finish composes the same `SetupRequest` fields as today, FR-024 no-capability-loss coverage.
- [x] **T023** [P] [US1] Rewrite `tests/StageFright.UI.Tests/Pages/Setup/SetupWizardNoSeederTests.cs` — same seeder-absent assertions, tabbed navigation instead of `#btn-next` step-counting.
- [x] **T024** [P] [US1] Rewrite `tests/StageFright.UI.Tests/Pages/Setup/SetupWizardThemeTests.cs` — same theme-toggle/Finish-composition assertions, tabbed navigation to the Review tab.
- [x] **T025** [P] [US1] New `tests/StageFright.UI.Tests/Pages/Setup/Tabs/GeneralAppearanceTabTests.cs`.
- [x] **T026** [P] [US1] New `tests/StageFright.UI.Tests/Pages/Setup/Tabs/MembershipFeesTabTests.cs`.
- [x] **T027** [P] [US1] New `tests/StageFright.UI.Tests/Pages/Setup/Tabs/SalesTaxTabTests.cs` — include the tax-toggle-off field-clearing case.
- [x] **T028** [P] [US1] New `tests/StageFright.UI.Tests/Pages/Setup/Tabs/CommitteeTabTests.cs` — legacy comma-separated field, pre-upgrade.
- [x] **T029** [P] [US1] New `tests/StageFright.UI.Tests/Pages/Setup/Tabs/ReviewTabTests.cs` — basic `dl` summary + relocated seed-data checkbox, pre-upgrade.

**Checkpoint**: The tabbed wizard is fully functional and independently testable — every field/rule/default from the old 5-step flow still works, just regrouped.

---

## Phase 4: User Story 4 — Queue Chart of Accounts entries during setup (Priority: P2, built here because US2/P1 depends on it)

**Goal**: A dedicated tab lets the coordinator queue new GL accounts during setup using the shared `AddAccountForm`, created together with the rest of setup at Finish — never required to finish.

**Independent Test**: Queue an account with name/type/(Asset) bank flag; confirm it's in the tab's bordered list but not yet in Finance ▸ Chart of Accounts; finish setup; confirm it now exists there.

**Wave 1:**

- [ ] **T030** [US4] Create `ChartOfAccountsTab.razor(.cs)` · `src/StageFright.UI/Pages/Setup/Tabs/` — hosts `AddAccountForm` (T003) with a queuing `OnSubmit` (appends to an in-memory list instead of calling `AccountService`) + `BorderedListBox` of queued accounts with remove; duplicate-name check passes `ExistingNames` = real accounts ∪ already-queued names (FR-012, FR-013, FR-014, FR-015).

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2:**

- [ ] **T031** [US4] Update `SetupWizard.razor(.cs)` — insert the Chart of Accounts tab at position 5 (per contract, must precede Opening Balances); add `List<QueuedAccountRequest>` queue state + add/remove handlers; Finish populates `SetupRequest.QueuedAccounts`; a failed Finish leaves the queue untouched (Edge Cases).

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — independent (different files):**

- [ ] **T032** [P] [US4] New `tests/StageFright.UI.Tests/Pages/Setup/Tabs/ChartOfAccountsTabTests.cs` — add/duplicate/blank/remove, Asset-only bank flag.
- [ ] **T033** [P] [US4] Update `SetupWizardTests.cs` — Finish composes `QueuedAccounts` from the tab's queue; empty queue still finishes normally (FR-015).
- [ ] **T034** [P] [US4] Extend `V1_FirstRunSetupTests.cs` — new scenario: `InitializeAsync` with a non-empty `QueuedAccounts` request creates the account (real DB, `AccountRepository`), no duplicate created when re-run guard applies.

**Checkpoint**: Coordinators can queue accounts during setup; skipping this tab still completes setup with only system defaults.

---

## Phase 5: User Story 2 — Enter opening balances before finishing setup (Priority: P1) 🎯 MVP

**Goal**: A dedicated tab covering every eligible existing + queued account lets the coordinator post opening balances at Finish; Finish is blocked unless at least one non-zero balance was entered or "load sample data" is selected instead.

**Independent Test**: Enter a balance, finish, confirm the `OpeningBalance` journal entry exists with the right amounts. Separately: Finish rejected with nothing entered and sample data unselected; Finish succeeds with sample data selected and nothing entered.

**Wave 1 — independent (different files):**

- [ ] **T035** [P] [US2] Create `OpeningBalancesTab.razor(.cs)` · `src/StageFright.UI/Pages/Setup/Tabs/` — hosts `OpeningBalanceEntryForm` (T004) with `Accounts` = `IOpeningBalanceService.GetOpeningBalanceAccountsAsync()` result ∪ this session's queued accounts (using each `QueuedAccountRequest.ClientId` as its row key until Finish resolves it), `ShowAlreadyPostedWarning="false"` (first-run setup can never have a prior posting — research.md), as-at date defaulting to today (research.md — no `FinancialYearStartMonth` exists yet) (FR-017, FR-019).
- [ ] **T036** [P] [US2] Extend `DebugDataSeeder.cs` · `src/StageFright.App/Seeding/` — inject `IOpeningBalanceService`; post an explicit `RecordOpeningBalancesAsync` for the accounts it creates (at minimum the bank account) before `SeedHistoricalTransfersAsync` runs (FR-026). **No automated coverage**: `DebugDataSeeder` has zero existing test harness (its ~19 constructor dependencies make one disproportionate to this one-call change; spec 016's T032 set the same precedent for this exact file) — verify via `dotnet build` plus a manual Debug-build "Load sample data" run confirming Finance ▸ Trial Balance shows an `OpeningBalance` entry for the seeded accounts (part of T053's full verification).

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2:**

- [ ] **T037** [US2] Update `SetupWizard.razor(.cs)` — insert the Opening Balances tab at position 6 (after Chart of Accounts); queue state keyed by account reference (real `AccountId` or a queued account's `ClientId`), synced to the Chart of Accounts tab's queue (adding there adds a row here; removing there removes the row and any entered balance — FR-020); Finish populates `SetupRequest.QueuedOpeningBalances` + `OpeningBalanceAsAtDate`; Finish is blocked unless ≥1 non-zero queued balance OR the Review tab's seed-data checkbox is checked, with the coordinator able to tell the Opening Balances tab needs attention (FR-021); a failed Finish leaves both the balance queue and the account queue untouched (Edge Cases).

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — independent (different files):**

- [ ] **T038** [P] [US2] New `tests/StageFright.UI.Tests/Pages/Setup/Tabs/OpeningBalancesTabTests.cs` — rows for existing + queued accounts, negative amount, plug, sync when a queued account is added/removed on the Chart of Accounts tab.
- [ ] **T039** [P] [US2] Update `SetupWizardTests.cs` — Finish blocked with no balance + sample-data unselected; Finish succeeds with a balance entered; Finish succeeds with sample-data selected and no balance entered (FR-021, US2 Acceptance Scenarios 2–3).
- [ ] **T040** [P] [US2] Extend `V1_FirstRunSetupTests.cs` — new scenario: `InitializeAsync` with non-empty `QueuedOpeningBalances` posts one `OpeningBalance` `JournalEntry` with balanced `Transaction` lines (real DB, `GLRepository`/`JournalEntryRepository`), including a queued-account reference resolved to its just-created real account.

**Checkpoint**: Both P1 stories (US1 + US2) are complete — the wizard's actual MVP: tabbed, regrouped, and opening balances are a hard requirement of a complete setup.

---

## Phase 6: User Story 5 — Add committee office-holder roles one at a time (Priority: P2)

**Goal**: The Committee tab's comma-separated titles textbox becomes a type-and-"+"-to-add bordered list, matching FR-007's app-wide convention.

**Independent Test**: Add two titles one at a time via "+", confirm both listed, remove one, confirm only the remaining one submits with setup.

**Wave 1:**

- [ ] **T041** [US5] Update `CommitteeTab.razor(.cs)` — replace the `CommitteeOfficeHolderTitlesText` textbox with an entry field + "+" button + `BorderedListBox<string>` (T002) with per-row remove; reject blank/whitespace and case-insensitive duplicates without adding (FR-009, FR-010, FR-011).

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2:**

- [ ] **T042** [US5] Update `SetupWizard.razor(.cs)` — Finish composes `SetupRequest.CommitteeOfficeHolderTitles` directly from the new `List<string>` queue instead of comma-splitting `_model.CommitteeOfficeHolderTitlesText` (the request's shape is unchanged — data-model.md confirms only the entry mechanism changes, no `SetupRequest`/`SetupService` edit needed here).

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — independent (different files):**

- [ ] **T043** [P] [US5] Update `CommitteeTabTests.cs` (T028) — +/− widget behavior: blank rejected, case-insensitive duplicate rejected, remove works, ≥5 titles addable/removable with no dupes/blanks ever appearing (SC-004).
- [ ] **T044** [P] [US5] Update `SetupWizardTests.cs` — Finish composes the queued title list; empty queue still finishes with no titles, as today (US5 Acceptance Scenario 5).

**Checkpoint**: Committee roles are added/removed one at a time; the underlying `SetupRequest` contract is untouched.

---

## Phase 7: User Story 3 — Review every setting before finishing setup (Priority: P2)

**Goal**: The Review tab's committee-roles and queued-accounts summaries become `BorderedListBox` displays instead of plain text, per FR-006.

**Independent Test**: Fill every tab (including ≥1 role and ≥1 account), open Review, confirm every value — including both lists — is visible without navigating elsewhere.

**Wave 1:**

- [ ] **T045** [US3] Update `ReviewTab.razor(.cs)` — replace the plain-text committee-titles line and (once T031/T041 exist) queued-accounts line with two read-only `BorderedListBox` (T002) summaries; the rest of the `dl` settings summary from US1 is unchanged (FR-006).

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2:**

- [ ] **T046** [US3] Update `SetupWizard.razor(.cs)` — pass the queued-roles list (US5) and queued-accounts list (US4) into `ReviewTab` as parameters.

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — independent (different files):**

- [ ] **T047** [P] [US3] Update `ReviewTabTests.cs` (T029) — roles and accounts render as bordered lists, not comma-separated/plain text.
- [ ] **T048** [P] [US3] Update `SetupWizardTests.cs` — every tab's entered value (including both lists) visible on Review without navigating back (US3 Acceptance Scenario 1).

**Checkpoint**: The Review tab fully reflects the tabbed/queued redesign — FR-006/FR-007 satisfied end-to-end.

---

## Phase 8: User Story 6 — Use dropdowns and checkboxes consistent with the rest of the wizard (Priority: P3)

**Goal**: The theme control is a dropdown, not a toggle; every other yes/no setting is already a checkbox (confirmed, not changed).

**Independent Test**: Theme control is a Light/Dark dropdown that updates the wizard's own appearance immediately; every yes/no elsewhere renders as a checkbox.

**Wave 1:**

- [ ] **T049** [US6] Update `GeneralAppearanceTab.razor(.cs)` — replace the `RadzenSwitch` theme toggle with a dropdown (`InputSelect`/`<select>`) listing Light/Dark, applying via `ThemeProvider` immediately on change (FR-022). Verify (no code change expected) that every other boolean field across `SalesTaxTab`/`ReviewTab` already renders as `InputCheckbox` — they do since before this feature (FR-023).

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2:**

- [ ] **T050** [US6] Update `SetupWizardThemeTests.cs` (T024) — replace `[role=switch]` selectors/interactions with the dropdown's selection pattern; same Finish-composition assertions.

**Checkpoint**: All six user stories are independently functional. The tabbed, queued, bordered-list wizard matches the full spec.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [ ] **T051** Reconcile `contracts/setup-wizard-ui-contract.md` with whatever parameter names implementation actually settled on (the contract's own preamble invites this) — in particular `AddAccountForm`'s duplicate-check parameter from T003.
- [ ] **T052** [P] Add a short "List box standards" note to CLAUDE.md's Tech Stack & Conventions section, mirroring the existing "Data grid standards" entry — `BorderedListBox` is now the one convention every list box in the app follows (FR-007).
- [ ] **T053** Full verification: `dotnet build` and `dotnet test` (no `--no-build`) across all five test projects; report pass/fail counts per CLAUDE.md's Build & Test Verification rule. Includes the manual Debug-build "Load sample data" walkthrough noted in T036.

---

## Dependencies & Execution Order

- **Setup (Phase 1, T001)** → **Foundational (Phase 2, T002–T015)**: Foundational blocks every story — Wave 1 (T002–T005) is 4 independent new files; Wave 2 (T006–T008) is 3 independent refactors/extensions, each keyed to one Wave 1 file; Wave 3 (T009–T015) is 7 independent test files.
- **Foundational → US1 (Phase 3, T016–T021)**: Wave 1 (T016–T020) is 5 independent new tab components; Wave 2 (T021) rewrites `SetupWizard` to host them all, so it waits for the full wave; Wave 3 (T022–T029) is 8 independent test files.
- **US1 → US4 (Phase 4, T030–T034)**: Wave 1 (T030) new tab; Wave 2 (T031) wires it into `SetupWizard` (same shared files T021 touched, hence sequential across phases); Wave 3 (T032–T034) is 3 independent test files.
- **US1 + US4 → US2 (Phase 5, T035–T040)**: Wave 1 (T035, T036) is 2 independent files; Wave 2 (T037) wires the tab into `SetupWizard` and adds Finish-gating; Wave 3 (T038–T040) is 3 independent test files. This is the second P1/MVP story — after this phase the wizard's hard opening-balance requirement is live.
- **US1 → US5 (Phase 6, T041–T044)**: independent of US4/US2's phases; only needs US1's `CommitteeTab` and Foundational's `BorderedListBox`.
- **US4 + US5 → US3 (Phase 7, T045–T048)**: needs both queued-list shapes to summarize; Wave 1 (T045) → Wave 2 (T046, same `SetupWizard` files) → Wave 3 (T047–T048, 2 independent test files).
- **US1 → US6 (Phase 8, T049–T050)**: independent of every other story; deliberately last per its own "lowest risk/value, first to defer" framing.
- **All stories → Polish (Phase 9, T051–T053)**.
